# El bucle de reescritura para agentes

IAdetector está pensado para que un agente (Claude Code, otro LLM con acceso MCP…)
mida un texto, lo reescriba guiado por las métricas y vuelva a medir, iterando hasta
que la probabilidad de IA baje a lo que el usuario pida. El servidor **nunca reescribe**:
mide, explica y compara; reescribir es trabajo del agente que consume las tools.

## El bucle

1. **`explain_detection(texto)`** — antes de tocar nada. Fíjate en:
   - `signalsWithData`: si es 0, no hay evidencia (texto demasiado corto o modelos no
     descargados) y reescribir «a ciegas» no tiene sentido.
   - `prioritizedActions`: las señales fuera del rango humano, ordenadas por
     `logitContribution` (β × puntuación). La primera es la que más empuja el veredicto:
     atácala primero; cada señal trae su guía en `recommendation`.
   - `reference` (p10/mediana/p90): el rango típico de esa señal en texto humano del
     corpus de calibración. El objetivo razonable es entrar en el rango, no llegar a un
     número mágico.
2. **Reescribe** aplicando la guía de las 1–2 señales con mayor contribución. Cambios
   pequeños y con sentido: la salvaguarda del paso 3 castiga los cambios drásticos.
3. **`compare_versions(original, reescritura)`** — decide con su `outcome`:
   - `mejora`: la probabilidad bajó y el texto conserva su vocabulario de contenido.
     Sigue iterando desde esta versión o para, según el objetivo.
   - `empeora`: descarta la reescritura y prueba otro enfoque sobre la anterior.
   - `sin_cambio`: tus cambios no mueven las señales; sube de nivel (otra señal).
   - `no_concluyente`: lee `warnings`. Si la similitud semántica cayó bajo 0,5, la
     reescritura se alejó demasiado del original: vuelve atrás y reescribe más cerca.
     Si falta evidencia en alguna versión, el bucle no es fiable con ese texto.
4. Repite hasta el objetivo o hasta que las mejoras se estanquen (2–3 iteraciones sin
   bajar suelen significar que el texto ya está en su suelo para este detector).

## Límites honestos

- El «0 %» significa 0 % **en este detector**. Otros detectores miden otras cosas;
  ninguna herramienta puede prometer invisibilidad universal, y este documento no lo hace.
- La calibración y los rangos humanos vienen de HC3 (respuestas de ChatGPT de 2023
  frente a texto humano en inglés, sobre todo de Wikipedia). En español o en otros
  dominios las cifras son menos fiables — el falso positivo en español está documentado
  y el soporte de español es trabajo futuro (ver `docs/CALIBRATION.md`).
- La similitud de `compare_versions` es Jaccard sobre palabras de contenido (≥4 letras):
  una heurística léxica para frenar reescrituras que vacían el texto, no una prueba de
  que el significado se conserva. La revisión final del significado es del agente y,
  en última instancia, de la persona.
- Reescribir para engañar a un detector tiene usos legítimos (prosa propia que suena
  genérica, aprendizaje de estilo) y usos que no lo son (suplantar autoría). El detector
  es evidencia probabilística, nunca prueba, en ambas direcciones.
