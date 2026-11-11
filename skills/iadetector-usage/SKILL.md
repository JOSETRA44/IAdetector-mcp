---
name: iadetector-usage
description: Explica cómo interpretar y comunicar los resultados del servidor MCP IAdetector (detect_text, detect_file, batch_detect_directory, explain_detection, compare_versions) — qué significan el veredicto, la probabilidad de IA y la confianza, cuándo cada tool es la indicada, cómo iterar reescribiendo con explain_detection + compare_versions, y cómo transmitir el resultado sin sobre-afirmar certeza. Usa esta skill SIEMPRE que llames a cualquier tool de IAdetector, o cuando el usuario pregunte si un texto/archivo/documento fue escrito por IA, "suena a ChatGPT", pida un chequeo de plagio/contenido generado, o quiera saber si algo es "auténtico"/humano — aunque no mencione IAdetector por nombre. El contrato completo está en docs/AGENT_GUIDE.md del repositorio.
---

# Interpretar resultados de IAdetector

IAdetector estima la probabilidad de que un texto haya sido generado por IA combinando siete señales (estadísticas + dos modelos ONNX locales), **sin llamar a ninguna API externa**. Es una estimación heurística, no una prueba — comunícalo así al usuario.

## Qué tool usar

| Situación | Tool |
|---|---|
| Texto pegado directamente en la conversación (hasta ~20 000 caracteres) | `detect_text` |
| Un archivo local específico | `detect_file` |
| Varios archivos de un directorio | `batch_detect_directory` |
| El usuario quiere saber *por qué* (desglose señal por señal) | `explain_detection`, o `detect_text`/`detect_file` con `verbose: true` |
| Iterar reescribiendo para bajar la probabilidad de IA | `explain_detection` (diagnóstico) → reescribes tú → `compare_versions` (validas) — ver `docs/AGENT_LOOP.md` |

Si el texto/archivo excede el límite, la tool lo dice explícitamente en el error — no lo trunques tú mismo primero, deja que la tool decida (fragmenta automáticamente textos largos, muestreando si son muy grandes).

## Cómo leer el resultado

```json
{
  "verdict": "LikelyAI",
  "aiProbability": 0.66,
  "confidence": 0.28,
  "signals": [...],
  "chunkInfo": null
}
```

**`verdict`** — umbral fijo sobre `aiProbability`:

| Rango | Veredicto |
|---|---|
| < 20% | Human |
| 20–40% | LikelyHuman |
| 40–60% | Mixed |
| 60–80% | LikelyAI |
| ≥ 80% | AI |

**`confidence`** no es "qué tan seguro estoy de que es IA" — es **qué tan de acuerdo están las señales entre sí**. Un veredicto extremo (AI o Human) con confianza BAJA significa que las señales se contradicen (ej. el clasificador ONNX dice 100% IA pero las señales estadísticas no ven nada raro) — trata ese caso como más incierto de lo que el veredicto solo sugiere, y dilo explícitamente si el usuario está tomando una decisión importante con el resultado.

**`signals`** (solo si pediste `verbose`) — cada señal trae `normalizedScore` (0–1, su propia opinión sobre si es IA), `weight` (cuánto pesó en el resultado final), `explanation` en texto y **`hasData`**: si es `false`, esa señal no midió (texto corto o modelo no descargado) y su 0,5 es una convención, no una medición. Si *ninguna* señal tiene datos, el resultado es `Mixed / 0,5 / confianza 0` — el detector diciendo "no lo sé". Con `explain_detection` cada señal trae además `logitContribution` (β × puntuación: cuánto empuja el veredicto), el rango humano de referencia y `prioritizedActions` con lo que más conviene atacar primero; el resultado incluye `detectedLanguage` y `warnings` de fiabilidad que debes respetar (en especial en español).

**`chunkInfo`** — presente solo si el texto era largo y se fragmentó. Si `sampled: true`, el documento era tan grande que se analizó una muestra representativa, no el texto completo; menciónalo si es relevante (ej. un análisis muy rápido sobre un archivo enorme).

## Señales sin modelo descargado

Dos de las siete señales (`onnx-classifier`, `perplexity`) requieren modelos ONNX descargados aparte (`iadetector models download`). Si no están descargados, esas señales quedan neutrales con peso reducido — la detección sigue funcionando con las cinco señales estadísticas, pero es menos confiable. Si ves varias señales en "no descargado" y el usuario necesita más precisión, sugiere ejecutar `iadetector models download --model roberta-openai-detector` y `--model distilgpt2-perplexity`.

## Limitaciones a comunicar honestamente

- El clasificador ONNX (RoBERTa) fue entrenado para detectar salidas de **GPT-2** (2019); generaliza de forma imperfecta a estilos de LLMs más recientes. Un texto de un modelo moderno puede pasar desapercibido para esa señal específica — por eso el ensemble combina varias señales, no confía en una sola.
- La calibración principal es de inglés (HC3): accuracy 0,97. El español tiene perfil propio calibrado (Fase 3.2, accuracy agrupada 0,91): funciona, pero con más cautela — la propia tool lo avisa en `warnings`. Otros idiomas no están validados. El "0%" de un bucle de reescritura es 0% frente a *este* detector, nunca indetectabilidad universal.
- Es una estimación probabilística de un sistema heurístico, no una prueba forense. Nunca lo presentes como un hecho definitivo ("esto ES IA"); preséntalo como lo que es ("el detector estima 66% de probabilidad de IA, veredicto LikelyAI, confianza 28%").

## Cómo responder al usuario

Prioriza claridad sobre jerga: menciona el veredicto y el porcentaje en lenguaje natural, y solo entra en el desglose de señales si el usuario lo pide o si la confianza es baja y vale la pena explicar por qué. Ejemplo de una buena respuesta:

> Este texto tiene un 66% de probabilidad estimada de haber sido generado por IA (veredicto: probablemente IA), aunque la confianza es baja (28%) — las señales no están del todo de acuerdo entre sí. El clasificador ONNX lo marca con alta probabilidad, pero las señales estadísticas (repetición, variación de oraciones) no ven nada fuera de lo común.

Evita: "Esto fue escrito por IA." (afirmación sin matices) o pegar el JSON crudo sin traducirlo a lenguaje natural salvo que el usuario pida explícitamente los datos completos.
