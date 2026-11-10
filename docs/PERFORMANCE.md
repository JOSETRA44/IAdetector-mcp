# Rendimiento e integridad (Fase 4)

Qué se midió, qué cambió y cuánto cuesta. Procedimiento reproducible: la herramienta
`tools/IAdetector.Benchmarks` genera textos sintéticos deterministas de varios tamaños
y mide la detección de extremo a extremo sin caché (cómputo puro), la primera llamada
en frío (incluye la carga de los dos modelos ONNX) y el acierto de caché.

## El problema de fondo: las señales ONNX no veían el texto entero

Cada modelo tiene una ventana fija — RoBERTa clasifica hasta 510 tokens de contenido y
la señal de perplexity medía 256 — y lo que no cabía **se truncaba en silencio a la
primera ventana**. En un ensayo de 800 palabras (~1.040 tokens), el clasificador veía
la mitad y perplexity la cuarta parte; en un documento fragmentado era peor: cada
fragmento de 2.000 palabras aportaba a las señales ONNX solo sus primeras ~390/195
palabras. El resto del texto influía únicamente en las señales estadísticas.

## Cambios

1. **Ventanas repartidas** (`TokenWindows`): si el texto excede la ventana, se evalúan
   varias ventanas distribuidas uniformemente de principio a fin y se agrega —
   RoBERTa promedia la probabilidad entre hasta 4 ventanas; perplexity agrega la
   log-probabilidad ponderada por tokens entre hasta 2 (512 tokens medidos). Perplexity
   usa menos porque cada ventana suya cuesta varias veces más (barrido del vocabulario
   completo, 50.257, por posición) y la literatura de la señal trabaja con 256–512
   tokens. Textos que caben en una ventana: comportamiento idéntico al anterior.
2. **Fragmentos en paralelo**: `DetectTextUseCase` analiza los fragmentos de un
   documento largo en paralelo con concurrencia acotada (máx. 4), agregando en el
   orden original — el resultado es el mismo que en secuencial, sin pagar la suma de
   latencias en serie en máquinas con varios núcleos.
3. **Integridad de modelos, también después de descargar**: la descarga ya verificaba
   el SHA-256; ahora `models download` verifica además los archivos que ya existen y
   vuelve a descargar los corruptos (antes, un archivo dañado se daba por bueno para
   siempre solo por existir), y existe `iadetector models verify` para auditar los dos
   modelos contra el catálogo en cualquier momento.
4. **La caché distingue la versión del motor**: la huella de la caché ya incluía pesos
   y modelos; ahora incluye también `engine=4`, así un cambio de semántica del análisis
   (como el punto 1) invalida las entradas viejas en vez de servir veredictos calculados
   con el comportamiento anterior.
5. **Bucle de perplexity en float**: el log-sum-exp por posición usa `MathF` en vez de
   `double`; la precisión sobra para un promedio entre cientos de tokens.

## Medición (VM de desarrollo, 1,5 vCPU compartidas — valores relativos)

Texto sintético, mediana en caliente de 5 repeticiones, sin caché:

| Palabras | Antes (truncado, secuencial) | Después (ventanas, paralelo) | Prob. IA antes → después |
|---|---|---|---|
| 150 | 1.021 ms | 800 ms | 0,135 → 0,135 (idéntica) |
| 800 | 1.682 ms | 2.984 ms | 0,582 → 0,124 |
| 3.000 (2 fragmentos) | 2.777 ms | 5.713 ms | 0,208 → 0,108 |
| 12.000 (7 fragmentos) | 8.423 ms | 18.099 ms | 0,028 → 0,015 |

La primera llamada en frío (carga de ambos modelos) costó ~11 s en la línea base y
~3,4 s tras los cambios en esta máquina (ruido de VM compartida; tómalo como orden de
magnitud). El acierto de caché sigue en 0–65 ms. Las probabilidades del benchmark son
de texto sintético mezclado, sin verdad de referencia: sirven para ver que el motor
responde distinto donde antes truncaba, no para juzgar acierto — eso se mide abajo
con los corpus reales.

La lectura honesta: **más cobertura cuesta tiempo de CPU**. Con ventanas 4+4 la
configuración inicial medía ~2,7–3,2× la línea base en textos largos; el ajuste a
4 (clasificador) + 2 (perplexity) lo deja en ~1,8–2,2×, que es el equilibrio elegido.
En máquinas con más núcleos, el paralelismo de fragmentos compensa parte del costo;
en esta VM de 1,5 vCPU apenas puede. El caso típico del bucle de agentes (un párrafo
de ~150 palabras) incluso bajó un poco, por el bucle de perplexity en float.

## Impacto en los veredictos (medido, no supuesto)

Re-extracción con el motor nuevo de dos muestras guardadas antes del cambio:

- **Respuestas más largas de HC3** (83 textos de 375–695 palabras, los que más
  truncaban): **0 veredictos cambiados**; accuracy idéntica (0,9639) antes y después.
  Las señales sí se movieron donde debían — |Δ| medio de perplexity 0,043, máximo
  0,27; |Δ| medio de probabilidad 0,007, máximo 0,09 — pero ningún texto cruzó el
  umbral. La calibración de inglés se midió sobre respuestas en su mayoría cortas, y
  en las largas el cambio de cobertura no altera conclusiones.
- **Corpus de evaluación en español de la Fase 3** (39 textos de 150–230 palabras):
  las features extraídas son **byte a byte idénticas** antes y después — esos textos
  caben enteros en ambas ventanas, así que el perfil español de la Fase 3.2 se calibró
  exactamente bajo las condiciones nuevas.

## Lo que no cambia

- Textos que caben en la ventana de cada modelo producen exactamente las mismas
  señales que antes (la probabilidad del texto de 150 palabras del benchmark es
  idéntica: 0,135).
- Ni los pesos de inglés ni los de español se tocaron en esta fase.
- La caché de resultados, el muestreo de documentos enormes (máx. 20 fragmentos) y
  los mínimos de evidencia por señal quedan como estaban.
