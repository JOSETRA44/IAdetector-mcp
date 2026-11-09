# Calibración del ensemble

Cómo se obtienen y se evalúan los coeficientes de `CalibratedEnsembleScorer`
(`src/IAdetector.Infrastructure/Ensemble/EnsembleWeights.json`), y qué significan
— y qué no — las cifras que se citan sobre la precisión del detector.

## Procedimiento

1. **Extracción de features** con la herramienta de desarrollo
   (`tools/IAdetector.Calibration`), que reutiliza el motor real de detección:

   ```
   dotnet run --project tools/IAdetector.Calibration -c Release -- \
       scripts/calibration/data/wiki_csai.jsonl scripts/calibration/data/features.csv
   ```

   Requiere los dos modelos ONNX descargados (`iadetector models download`).
   El CSV resultante trae, por fila: `question_id` (la pregunta HC3 de origen),
   `source` (el dataset), `label` (0 = humano, 1 = IA), las 7 señales y, por
   señal, `<señal>_hasdata` (1 si la señal midió el texto de verdad, 0 si
   devolvió el 0.5 convencional de «sin datos»). Todos los números se escriben
   con cultura invariante.

2. **Ajuste y evaluación** con `scripts/calibration/fit_logistic_regression.py`
   (scikit-learn):

   ```
   python fit_logistic_regression.py <features.csv> <salida.json>
   ```

   La evaluación usa predicciones fuera de fold con **GroupKFold(5) agrupando
   por pregunta**: las respuestas a una misma pregunta nunca quedan a ambos
   lados de un split. Se reportan accuracy, Brier, ECE (error de calibración
   esperado, 10 contenedores) y AUROC; a modo de contraste, también las mismas
   métricas con KFold por filas, comparables con la calibración original.
   La salida JSON incluye el bloque `evaluation` con ambas.

3. **Regla de publicación**: informe primero, pesos después. Un reajuste solo
   sustituye a `EnsembleWeights.json` de producción cuando su evaluación
   agrupada mejora a la vigente o corrige un defecto documentado; si los
   coeficientes reproducen a los actuales, no se toca nada.

## Evaluación del 2026-11-09 (Fase 1.2)

Dataset: `wiki_csai.jsonl` (HC3), 842 preguntas, 1.684 respuestas
(842 humanas / 842 de ChatGPT). Features regeneradas desde cero con el motor
actual (incluye la Fase 1.1) y los dos modelos ONNX descargados.

**Cobertura real de cada señal** (la novedad que destapó la Fase 1.2: hasta
ahora, un 0.5 en el CSV no distinguía «medido» de «sin datos»):

| Señal | Con datos | Cobertura |
|---|---|---|
| onnx-classifier | 1.678 / 1.684 | 99,6% |
| perplexity | 1.678 / 1.684 | 99,6% |
| burstiness | 1.370 / 1.684 | 81,4% |
| ngram-repetition | 672 / 1.684 | 39,9% |
| compression-ratio | 176 / 1.684 | 10,5% |
| type-token-ratio | 66 / 1.684 | **3,9%** |
| lexical-entropy | 66 / 1.684 | **3,9%** |

Las respuestas de HC3 son cortas: riqueza léxica y entropía exigen 400 palabras
y compresión ~350, así que en la práctica el ensemble se calibra — y decide —
casi solo con burstiness y los dos modelos ONNX. Cualquier conclusión sobre
esas tres señales estadísticas debe leerse con esa cobertura en mente.

**Métricas fuera de fold:**

| Evaluación | Accuracy | Brier | ECE | AUROC |
|---|---|---|---|---|
| GroupKFold por pregunta | 0,9697 | 0,0257 | 0,0220 | 0,9924 |
| KFold por filas (como antes) | 0,9679 | 0,0262 | 0,0188 | 0,9918 |
| Cifra histórica (por filas, 2026) | 0,9685 | — | — | — |

Conclusión honesta: la validación por filas era metodológicamente incorrecta,
pero en **este** dataset la fuga entre folds no infló la cifra de forma
apreciable (cada pregunta aporta exactamente una respuesta humana y una de IA,
y las señales generalizan entre preguntas). La accuracy agrupada, 96,97%, es
la que debe citarse a partir de ahora, junto con Brier 0,026 y ECE 0,022:
las probabilidades que emite el ensemble están bien calibradas sobre este
corpus.

**Reajuste de control:** entrenar de nuevo sobre las features regeneradas
reproduce los coeficientes de producción casi exactamente (intercept −7,705
frente a −7,709; todos los pesos dentro de ±0,04). Los pesos vigentes son
consistentes con los datos: **no se sustituyen**.

## Límites de estas cifras

- Son sobre HC3 (ChatGPT de 2023 contra respuestas de wiki/open-qa). No miden
  el rendimiento frente a otros generadores (GPT-4o, Claude, Gemini, Llama),
  otros dominios ni texto en español, para el que hoy no hay validación.
- La cobertura de las señales estadísticas depende de la longitud: en textos
  de 400+ palabras su peso real en la decisión será mayor que aquí.
- La evaluación held-out entre dominios vive en
  `tests/IAdetector.Integration.Tests` (`open_qa.jsonl`, nunca usado para
  calibrar; exige accuracy ≥ 0,80 y se omite con motivo explícito si faltan
  los modelos o el dataset). Ejecutada de verdad el 2026-11-09 con los
  modelos descargados: **superada** (300 respuestas, ~6 minutos de inferencia).
