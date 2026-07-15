using System.Text.Json;
using IAdetector.Core.Abstractions;
using IAdetector.Infrastructure.Serialization;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

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

    private readonly Lazy<LoadedModel> _loaded = new(() => Load(cache), LazyThreadSafetyMode.ExecutionAndPublication);

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

    private static LoadedModel Load(IModelCache cache)
    {
        var dir = cache.GetPath(ModelCatalog.RobertaOpenAiDetector);

        var session = new InferenceSession(Path.Combine(dir, "model.onnx"));

        // Se parsea vocab.json/merges.txt a mano (en vez de usar el constructor de BpeOptions que
        // recibe rutas de archivo) porque ese constructor deserializa el JSON por reflexión
        // internamente, lo cual lanza InvalidOperationException bajo Native AOT (reflexión
        // deshabilitada). JsonSerializer.Deserialize aquí usa el contexto source-generated.
        var vocabJson = File.ReadAllText(Path.Combine(dir, "vocab.json"));
        var vocabulary = JsonSerializer.Deserialize<Dictionary<string, int>>(vocabJson, DetectionJson.Options)
            ?? throw new InvalidOperationException("vocab.json vacío o inválido.");
        var merges = File.ReadAllLines(Path.Combine(dir, "merges.txt"))
            .Where(line => line.Length > 0 && !line.StartsWith("#version", StringComparison.Ordinal));

        // ByteLevel=true es imprescindible: sin él, BpeTokenizer no antepone el marcador de
        // espacio byte-level ('Ġ') a las palabras que siguen a un espacio y tokeniza distinto a
        // como fue entrenado el modelo (confirmado: "world" da id 8331 en vez del correcto 232
        // de "Ġworld"). RobertaPreTokenizer aporta el regex de segmentación correcto para RoBERTa.
        var options = new BpeOptions(vocabulary)
        {
            Merges = merges,
            PreTokenizer = RobertaPreTokenizer.Instance,
            ByteLevel = true,
        };
        var tokenizer = BpeTokenizer.Create(options);

        var inputIdsName = session.InputMetadata.Keys.FirstOrDefault(n => n.Equals("input_ids", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("El modelo ONNX no expone una entrada 'input_ids'.");
        var attentionMaskName = session.InputMetadata.Keys.FirstOrDefault(n => n.Equals("attention_mask", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("El modelo ONNX no expone una entrada 'attention_mask'.");
        var outputName = session.OutputMetadata.Keys.FirstOrDefault(n => n.Contains("logits", StringComparison.OrdinalIgnoreCase))
            ?? session.OutputMetadata.Keys.First();

        return new LoadedModel(session, tokenizer, inputIdsName, attentionMaskName, outputName);
    }

    public void Dispose()
    {
        if (_loaded.IsValueCreated)
        {
            _loaded.Value.Session.Dispose();
        }
    }

    private sealed record LoadedModel(InferenceSession Session, Tokenizer Tokenizer, string InputIdsName, string AttentionMaskName, string OutputName);
}
