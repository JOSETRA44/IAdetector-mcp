using IAdetector.Core.Abstractions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace IAdetector.Infrastructure.Ml;

/// <summary>
/// Clasificador RoBERTa (onnx-community/roberta-base-openai-detector-ONNX, MIT) vía ONNX Runtime
/// nativo. Carga perezosa (sesión + tokenizador) en el primer uso; requiere que el modelo ya esté
/// descargado (ver <see cref="IModelCache"/>/<see cref="IModelDownloader"/>).
/// </summary>
public sealed class RobertaOnnxClassifier(IModelCache cache) : IOnnxClassifier, IDisposable
{
    // 512 posiciones efectivas (max_position_embeddings=514 en config.json, menos el offset de
    // RoBERTa) menos 2 para los tokens especiales <s> (BOS) y </s> (EOS).
    private const int MaxContentTokens = 510;
    private const int BosTokenId = 0;
    private const int EosTokenId = 2;

    // config.json del modelo: id2label = {"0": "Fake", "1": "Real"}. "Fake" = generado por IA.
    private const int AiLabelIndex = 0;

    private readonly Lazy<Gpt2StyleModelLoader.Loaded> _loaded = new(
        () => Gpt2StyleModelLoader.Load(cache.GetPath(ModelCatalog.RobertaOpenAiDetector), "logits"),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public string ModelId => ModelCatalog.RobertaOpenAiDetector;

    public Task<float> ClassifyAsync(string text, CancellationToken cancellationToken = default) =>
        Task.Run(() => Classify(text), cancellationToken);

    private float Classify(string text)
    {
        var model = _loaded.Value;

        var contentIds = model.Tokenizer.EncodeToIds(text);
        var truncated = contentIds.Count > MaxContentTokens ? contentIds.Take(MaxContentTokens) : contentIds;

        var ids = new List<long> { BosTokenId };
        ids.AddRange(truncated.Select(id => (long)id));
        ids.Add(EosTokenId);

        var inputIds = new DenseTensor<long>(ids.ToArray(), [1, ids.Count]);
        var attentionMask = new DenseTensor<long>(Enumerable.Repeat(1L, ids.Count).ToArray(), [1, ids.Count]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(model.InputIdsName, inputIds),
            NamedOnnxValue.CreateFromTensor(model.AttentionMaskName, attentionMask),
        };

        using var results = model.Session.Run(inputs);
        var logits = results.First(r => r.Name == model.OutputName).AsTensor<float>().ToArray();

        return Softmax(logits)[AiLabelIndex];
    }

    private static float[] Softmax(float[] logits)
    {
        var max = logits.Max();
        var exps = logits.Select(l => MathF.Exp(l - max)).ToArray();
        var sum = exps.Sum();
        return exps.Select(e => e / sum).ToArray();
    }

    public void Dispose()
    {
        if (_loaded.IsValueCreated)
        {
            _loaded.Value.Session.Dispose();
        }
    }
}
