# Español: medición primero (Fase 3)

La calibración del ensemble y los dos modelos ONNX son de **inglés**. Antes de tocar
nada, esta fase mide qué hace el detector hoy con texto en español, porque la
anécdota disponible (un falso positivo del 60 % en un texto humano, del plan de
Antigravity) no bastaba para diseñar la corrección.

## El corpus de evaluación

`scripts/calibration/data/es/es_eval.jsonl` — 39 textos de 150–230 palabras:

- **21 humanos**: muestras de dominio público de Project Gutenberg — *Don Quijote*
  (#2000), *Lazarillo de Tormes* (#320) y *La Regenta* (#17073), 7 por obra, tomadas a
  intervalos regulares. Procedencia en `es_eval.jsonl.provenance.txt`.
- **18 de IA**: textos expositivos en español generados por un LLM (Muse Spark) el
  2026-11-09, sobre temas variados (educación, clima, salud, turismo…), etiquetados
  como lo que son.

Extracción con la herramienta de calibración (`features_before.csv`, commiteado) y
evaluación replicando el ensemble de producción con
`scripts/calibration/evaluate_spanish.py` (la réplica se validó contra HC3:
accuracy 0,9691 frente al 0,9697 del GroupKFold de la Fase 1.2).

**Sesgos declarados del corpus**: la prosa humana es clásica (siglos XVI–XIX), no
prosa moderna ni estudiantil; los textos de IA son de un solo generador y de registro
expositivo. Las cifras miden *este* corpus; la dirección del problema, no obstante,
es estructural (ver el mecanismo) y no depende del registro.

## Resultado: ceguera, no falsos positivos

| | n | Prob. IA media | ≥ 0,5 |
|---|---|---|---|
| Humanos | 21 | 0,004 | 0/21 (0 % falsos positivos) |
| IA | 18 | 0,012 | **0/18 detectados (100 % de falsos negativos)** |

Por señal (media de la puntuación normalizada, solo muestras con datos):

| Señal | Humanos | IA | ¿Mide en el corpus? |
|---|---|---|---|
| burstiness | 0,339 | 0,831 | 13/21 humanos, 18/18 IA |
| onnx-classifier | 0,000 | 0,000 | todos |
| perplexity | 0,000 | 0,000 | todos |

TTR, entropía y compresión no midieron ninguna muestra (los textos de 150–230
palabras quedan bajo sus mínimos, como ya pasaba en HC3); n-gramas, solo 3 humanas.

**El mecanismo**: el clasificador RoBERTa (detector de salidas de GPT-2 en inglés) y
la perplexity de DistilGPT-2 puntúan ≈0 ante *cualquier* texto en español — humano o
de IA — porque «no parece salida inglesa de GPT-2» es su única lectura. Como entre
los dos suman β ≈ 9,7 de un logit con intercept −7,7, su voto conjunto de «humano»
entierra la única señal que sí separa en español (burstiness: 0,34 frente a 0,83).
La anécdota del falso positivo no se reproduce con prosa clásica; puede ocurrir en
otros registros, pero el fallo dominante y estructural es el contrario: **en español,
el detector casi no detecta IA**.

## Por qué NO se «arregló» abstenerse (medido, no opinado)

La corrección tentadora — que los modelos ONNX se abstengan en español
(`HasData=false`) — empeora las cosas con la mecánica actual del ensemble: una señal
sin datos entra al logit imputada a 0,5, y 0,5 × 9,7 ≈ +4,85 de logit empujaría a
*los humanos* en español hacia ≈50 % de probabilidad de IA (estimación con la
burstiness media humana: logit ≈ +0,08). Se cambiaría ceguera por falsos positivos.
La abstención solo es correcta junto a una **recalibración** que reequilibre el
intercept y los pesos para español.

## Qué hace esta fase (sin tocar coeficientes)

- **Detección de idioma** (`LanguageDetector`, Core): frecuencia de palabras
  funcionales ES/EN, con umbrales de evidencia y de mayoría; devuelve `Unknown` ante
  la duda. `DetectionResult` ahora lleva `detectedLanguage` (también en la caché).
- **Aviso de fiabilidad**: `explain_detection` incluye `language` y `warnings`; en
  español el aviso dice explícitamente que un veredicto «humano» no es fiable hoy y
  por qué, remitiendo a este documento.
- Corpus, `features_before.csv` y script de evaluación commiteados: la línea base
  contra la que se medirá cualquier calibración en español.

## La decisión que queda (Fase 3.2)

Calibrar un **perfil en español** (`EnsembleWeights.es.json` + enrutado por idioma en
el scorer) exige antes un corpus en español a escala y de registro moderno:

- Humanos modernos: textos con fecha previa a la era LLM (p. ej., Wikipedia en
  español en revisiones ≤2019, prensa y literatura contemporánea con licencia).
- IA: generaciones de varios modelos actuales sobre las mismas preguntas/temas.
- Objetivo mínimo razonable: ≥500 pares, para que el GroupKFold por tema/pregunta
  dé cifras defendibles como las de la Fase 1.2.

Alternativa o complemento: evaluar un clasificador multilingüe (tipo XLM-R) como
señal ONNX para español — decisión que, según la síntesis del proyecto, se toma
*después* de tener corpus y medición, no antes. Mientras tanto, el comportamiento
honesto es el de esta fase: medir lo medible, avisar de lo que no, y no vender el
veredicto en español como si fuera el de inglés.
