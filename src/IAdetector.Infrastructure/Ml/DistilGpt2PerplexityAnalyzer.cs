using IAdetector.Core.Abstractions;
using IAdetector.Core.Domain;
using IAdetector.Infrastructure.Signals;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace IAdetector.Infrastructure.Ml;

/// <summary>
/// Señal de perplexity bajo un modelo local pequeño (DistilGPT-2, ONNX, licencia Apache-2.0 vía
/// onnx-community): texto más "predecible" para el modelo (perplexity baja) es una señal débil
/// pero conocida de posible generación por IA (base conceptual de GLTR/Fast-DetectGPT/Binoculars,
/// aquí simplificada a perplexity de un solo modelo — no la razón de dos modelos del método
/// Binoculars completo, que requeriría un segundo modelo y duplicaría el costo/descarga).
/// Igual que el clasificador RoBERTa: no descarga el modelo automáticamente.
/// </summary>
public sealed class DistilGpt2PerplexityAnalyzer(IModelCache cache) : ISignalAnalyzer, IDisposable
{
    public string Name => "perplexity";

    // n_ctx=1024 en config.json, pero limitamos a 256 tokens: suficiente para una estimación de
    // perplexity representativa (Fast-DetectGPT/Binoculars también operan sobre ventanas acotadas)
    // sin pagar el costo de una matriz de logits [1, 1024, 50257] completa por cada detección.
    private const int MaxTokens = 256;
    private const int MinWords = 20;
    private const double Weight = 1.0;

    private readonly Lazy<Gpt2StyleModelLoader.Loaded> _loaded = new(
        () => Gpt2StyleModelLoader.Load(cache.GetPath(ModelCatalog.DistilGpt2Perplexity), "logits"),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public async Task<SignalScore> AnalyzeAsync(TextSample sample, CancellationToken cancellationToken = default)
    {
        if (!cache.IsDownloaded(ModelCatalog.DistilGpt2Perplexity))
        {
            return new SignalScore(
                Name, 0.0, 0.5, Weight * ScoreNormalization.InsufficientDataWeightFactor,
                $"Modelo '{ModelCatalog.DistilGpt2Perplexity}' no descargado. Ejecuta 'iadetector models download --model {ModelCatalog.DistilGpt2Perplexity}' para activar esta señal.", HasData: false);
        }

        if (sample.WordCount < MinWords)
        {
            return new SignalScore(
                Name, 0.0, 0.5, Weight * ScoreNormalization.InsufficientDataWeightFactor,
                $"Muy pocas palabras ({sample.WordCount}) para medir perplexity de forma confiable.", HasData: false);
        }

        var perplexity = await Task.Run(() => ComputePerplexity(sample.Text), cancellationToken).ConfigureAwait(false);
        var logPerplexity = Math.Log(perplexity);

        // Provisional (pendiente de calibración con más datos, igual que las señales estadísticas
        // de la Fase 1): log(perplexity) alto -> texto humano; bajo -> más predecible/IA-like.
        var score = ScoreNormalization.MapToProbability(logPerplexity, valueAtZero: 4.0, valueAtOne: 2.5);

        return new SignalScore(
            Name, perplexity, score, Weight,
            $"Perplexity (DistilGPT-2)={perplexity:F1}.");
    }

    private double ComputePerplexity(string text)
    {
        var model = _loaded.Value;

        var allIds = model.Tokenizer.EncodeToIds(text);
        var ids = allIds.Count > MaxTokens ? allIds.Take(MaxTokens).ToList() : allIds.ToList();

        var inputIds = new DenseTensor<long>(ids.Select(id => (long)id).ToArray(), [1, ids.Count]);
        var attentionMask = new DenseTensor<long>(Enumerable.Repeat(1L, ids.Count).ToArray(), [1, ids.Count]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(model.InputIdsName, inputIds),
            NamedOnnxValue.CreateFromTensor(model.AttentionMaskName, attentionMask),
        };

        if (model.PositionIdsName is { } positionIdsName)
        {
            var positionIds = new DenseTensor<long>(Enumerable.Range(0, ids.Count).Select(i => (long)i).ToArray(), [1, ids.Count]);
            inputs.Add(NamedOnnxValue.CreateFromTensor(positionIdsName, positionIds));
        }

        // Export "with-past": aunque es el primer forward pass (sin caché previo), el grafo exige
        // tensores past_key_values con la dimensión de secuencia en 0 (vacíos, no null).
        foreach (var pkv in model.PastKeyValues)
        {
            var empty = new DenseTensor<float>([1, pkv.NumHeads, 0, pkv.HeadDim]);
            inputs.Add(NamedOnnxValue.CreateFromTensor(pkv.Name, empty));
        }

        using var results = model.Session.Run(inputs);
        var logitsTensor = results.First(r => r.Name == model.OutputName).AsTensor<float>();
        var vocabSize = logitsTensor.Dimensions[2];

        // Acceso al buffer plano en vez del indexador multidimensional del tensor: con
        // ids.Count*vocabSize ~ 13M elementos por muestra, el indexador (recalcula strides en
        // cada llamada) hacía esto ~30-60x más lento de lo necesario.
        var span = ((DenseTensor<float>)logitsTensor).Buffer.Span;

        double totalNegLogProb = 0;
        var count = 0;

        // Perplexity estándar: para cada posición, cross-entropy entre la distribución predicha
        // (softmax de los logits) y el token real siguiente. Un solo forward pass (teacher forcing).
        for (var pos = 0; pos < ids.Count - 1; pos++)
        {
            var targetId = ids[pos + 1];
            var row = span.Slice(pos * vocabSize, vocabSize);

            var maxLogit = float.NegativeInfinity;
            for (var v = 0; v < vocabSize; v++)
            {
                if (row[v] > maxLogit)
                {
                    maxLogit = row[v];
                }
            }

            double sumExp = 0;
            for (var v = 0; v < vocabSize; v++)
            {
                sumExp += Math.Exp(row[v] - maxLogit);
            }

            var logSumExp = Math.Log(sumExp) + maxLogit;
            var logProbTarget = row[targetId] - logSumExp;

            totalNegLogProb += -logProbTarget;
            count++;
        }

        return Math.Exp(totalNegLogProb / count);
    }

    public void Dispose()
    {
        if (_loaded.IsValueCreated)
        {
            _loaded.Value.Session.Dispose();
        }
    }
}
