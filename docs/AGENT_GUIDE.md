# Guía para agentes — el contrato de IAdetector

Este documento es el contrato completo entre IAdetector y cualquier agente que lo
consuma (por MCP o por la CLI): qué devuelve cada tool, qué significa cada campo, qué
protocolo seguir para iterar reescribiendo y qué límites hay que respetar. Si solo vas
a leer un documento antes de usar las tools, que sea este. El detalle del bucle está
en [`AGENT_LOOP.md`](AGENT_LOOP.md); la calibración, en
[`CALIBRATION.md`](CALIBRATION.md); el español, en [`SPANISH.md`](SPANISH.md); el
rendimiento, en [`PERFORMANCE.md`](PERFORMANCE.md).

## Qué es (y qué no)

IAdetector estima la probabilidad de que un texto haya sido generado por IA combinando
siete señales con un ensemble calibrado por regresión logística. Es **100 % local**:
ninguna tool llama a una API externa y el texto analizado nunca sale de la máquina.
El servidor **mide, explica y compara — nunca reescribe**: reescribir es trabajo del
agente que consume las tools.

Una detección es **evidencia probabilística, no una prueba**. Ningún resultado de este
detector basta, por sí solo, para acusar a nadie de nada.

## Tools MCP

| Tool | Entrada | Para qué |
|---|---|---|
| `detect_text` | `text` (máx. 20 000 caracteres), `verbose` | Analizar un texto pegado |
| `detect_file` | `path` absoluto (archivo de texto, máx. 5 MB), `verbose` | Analizar un archivo |
| `batch_detect_directory` | `path`, `pattern` (`*.txt`), `recursive`, `maxFiles` (1–1 000, def. 100) | Muchos archivos de golpe |
| `explain_detection` | `text` (máx. 20 000 caracteres) | Diagnóstico prescriptivo por señal: el punto de partida del bucle |
| `compare_versions` | `originalText`, `rewrittenText` | Comparar original y reescritura, sin estado |

Todas son de solo lectura. Los errores (archivo inexistente, texto sobre el límite,
directorio sin archivos) llegan como error MCP con el motivo concreto: léelo entero,
no solo el prefijo genérico del SDK.

## El resultado de una detección

```json
{
  "verdict": "LikelyAI",
  "aiProbability": 0.66,
  "confidence": 0.28,
  "signals": [],
  "chunkInfo": null,
  "detectedLanguage": "English"
}
```

- **`aiProbability`** (0–1): la salida del ensemble calibrado. Es la cifra principal.
- **`verdict`**: bandas fijas sobre `aiProbability` — `<0,2` Human · `0,2–0,4`
  LikelyHuman · `0,4–0,6` Mixed · `0,6–0,8` LikelyAI · `≥0,8` AI.
- **`confidence`** (0–1): **no** es la seguridad del veredicto; es cuánto coinciden
  las señales entre sí (1 − dispersión ponderada). Un veredicto extremo con confianza
  baja = las señales se contradicen: trátalo como incierto y dilo.
- **`signals`**: vacío salvo que pidas `verbose: true` (o uses `explain_detection`).
  Cada señal: `name`, `rawValue`, `normalizedScore` (0–1, su voto), `weight` y
  `hasData`. **`hasData: false` significa que esa señal no midió** (texto corto,
  modelo no descargado) y su 0,5 es una convención, no una medición. Si *ninguna*
  señal tiene datos, el resultado es siempre `Mixed / 0,5 / confianza 0`: es el
  detector diciendo «no lo sé», no un veredicto humano.
- **`chunkInfo`**: presente si el texto se fragmentó (>2 500 palabras): `totalChunks`
  analizados, `totalWords`, y `sampled: true` si el documento era tan grande que se
  muestrearon 20 fragmentos repartidos en vez de analizarlos todos.
- **`detectedLanguage`**: `English`, `Spanish` o `Unknown` (detector por palabras
  funcionales). Determina qué perfil de calibración se aplicó; ver «Idiomas».

Las siete señales: `burstiness`, `type-token-ratio`, `lexical-entropy`,
`compression-ratio`, `ngram-repetition` (estadísticas) y `onnx-classifier`,
`perplexity` (modelos ONNX locales). Las dos últimas requieren descarga previa
(`iadetector models download`); sin modelos quedan sin datos y el ensemble funciona
con las cinco estadísticas, con menos precisión. Cada señal tiene mínimos de
evidencia (p. ej. riqueza léxica y entropía solo miden con ≥400 palabras): en textos
cortos es normal que varias queden sin datos.

## `explain_detection`: el diagnóstico prescriptivo

Devuelve el resultado más, por señal (`signals[]`):

- `hasData`, `rawValue`, `normalizedScore`
- `coefficient` (β calibrado) y **`logitContribution`** = β × puntuación: cuánto
  empuja esa señal el resultado. Ordena por ahí para saber qué atacar primero.
- `reference`: rango humano `{p10, median, p90, samples}` de esa señal en el corpus
  de calibración del idioma activo.
- `status`: `sin_datos` · `en_rango_humano` · `mas_ia_que_el_rango_humano` ·
  `mas_humano_que_lo_tipico` · `sin_referencia`.
- `recommendation`: qué cambiar en la reescritura cuando está fuera de rango.

Y a nivel global: `signalsWithData`/`signalsTotal`, **`prioritizedActions`** (las
señales fuera de rango, ordenadas por contribución), `language` y **`warnings`**
(avisos de fiabilidad: léelos y respétalos — en especial el de español).

## `compare_versions`: comparar sin estado

No guarda nada: pasa original y reescritura en cada llamada. Devuelve `before`,
`after`, `deltaProbability`, `signalDeltas[]` (con `logitDelta` = cuánto del cambio
explica cada señal), `semanticSimilarity`, `warnings` y **`outcome`**:

| `outcome` | Significado | Qué hacer |
|---|---|---|
| `mejora` | Bajó la probabilidad y se conserva el vocabulario | Seguir desde esta versión o parar |
| `empeora` | Subió | Descartar y probar otro enfoque |
| `sin_cambio` | No se movió | Atacar otra señal (ver `explain_detection`) |
| `no_concluyente` | Falta evidencia en alguna versión **o** la similitud cayó < 0,5 | Leer `warnings`; reescribir más cerca del original |

La salvaguarda es deliberada: si la reescritura pierde el vocabulario de contenido
del original (Jaccard de palabras de ≥4 letras < 0,5), el outcome es `no_concluyente`
**aunque la probabilidad baje a 0** — bajar la cifra destruyendo el texto no cuenta
como mejora. También avisa si la longitud cambió fuera del rango [0,5×, 2×].

## El protocolo del bucle (resumen)

1. `explain_detection` sobre el original. Si `signalsWithData` es 0, para: no hay
   evidencia con la que iterar.
2. Reescribe atacando las 1–2 señales de mayor `logitContribution`, con cambios
   pequeños y con sentido, buscando entrar en el rango humano — no un número mágico.
3. `compare_versions` tras cada reescritura; actúa según el `outcome`.
4. Para cuando llegues al objetivo del usuario o cuando 2–3 iteraciones seguidas no
   bajen: el texto está en su suelo para este detector.

El detalle completo, con ejemplos de decisiones, está en
[`AGENT_LOOP.md`](AGENT_LOOP.md).

## Idiomas y fiabilidad — lo que el agente debe saber

- **Inglés**: perfil calibrado sobre HC3 (1 684 textos, validación agrupada por
  pregunta): accuracy 0,9697, AUROC 0,9924.
- **Español**: perfil propio calibrado sobre un corpus de 568 textos (Fase 3.2):
  accuracy agrupada 0,9067. Menor que el inglés, y el propio `explain_detection` lo
  declara en `warnings`. La IA de ese corpus procede de pocos generadores; ante IA de
  otro generador la detección medida fue 12/18. Trata los resultados en español con
  más cautela y no prometas paridad con el inglés.
- **`Unknown`**: se aplica el perfil de inglés y va con aviso.
- Otros idiomas: sin validar. El detector de idioma solo distingue ES/EN.

## Caché, rendimiento y límites operativos

- Los resultados se cachean en disco con clave (texto + huella de pesos, modelos y
  versión del motor): repetir una detección es casi instantáneo (0–65 ms medidos).
- Sin caché, un párrafo (~150 palabras) cuesta ~0,8–1 s en caliente en hardware
  modesto; la primera llamada paga la carga de los modelos (~3–11 s). Textos largos
  cuestan más desde la Fase 4 porque las señales ONNX cubren todo el texto con
  ventanas repartidas, no solo el principio (ver `PERFORMANCE.md`).
- Límites duros: `detect_text`/`explain_detection` 20 000 caracteres; `detect_file`
  5 MB; lote 1 000 archivos por llamada.
- Integridad: `iadetector models verify` comprueba los SHA-256 de los modelos
  contra el catálogo; `models download` autorrepara archivos corruptos.

## La CLI como contrato alternativo

Todo lo anterior tiene equivalente en la CLI `iadetector` (detect, batch, models,
cache). Su manifiesto legible por máquina se obtiene con `iadetector describe
--json`: comandos, campos de salida, códigos de salida y qué comando usa red (solo
`models download`). Para un agente sin MCP, ese manifiesto es el contrato.

## Límites honestos (no negociables al comunicar)

- «0 %» significa 0 % **frente a este detector**. Otros detectores miden otras
  cosas; no existe la indetectabilidad universal y este proyecto no la promete.
- Los rangos humanos y la calibración vienen de corpus concretos (HC3 en inglés;
  corpus propio en español), no de «el texto humano» en abstracto.
- La similitud de `compare_versions` es una heurística léxica, no una prueba de que
  el significado se conserva: la revisión final del significado es del agente y de
  la persona.
- Los falsos positivos afectan de forma desproporcionada a quien escribe en un
  idioma que no es el suyo y a la prosa técnica formulaica. Comunica siempre
  probabilidad + confianza, nunca una acusación.
