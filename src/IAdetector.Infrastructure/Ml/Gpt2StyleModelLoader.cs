using System.Text.Json;
using IAdetector.Infrastructure.Serialization;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.Tokenizers;

namespace IAdetector.Infrastructure.Ml;

/// <summary>
/// Carga compartida por los modelos ONNX de la familia GPT-2/RoBERTa (RoBERTa-detector, Fase 2;
/// DistilGPT-2-perplexity, Fase 4): ambos usan el mismo esquema de tokenización byte-level BPE y
/// la misma convención de nombres de entrada/salida de HuggingFace Optimum.
/// </summary>
internal static class Gpt2StyleModelLoader
{
    /// <summary>Entrada de caché KV vacía requerida por exports "with-past" incluso en el primer forward pass (sin caché aún).</summary>
    public sealed record PastKeyValueInput(string Name, int NumHeads, int HeadDim);

    public sealed record Loaded(
        InferenceSession Session,
        Tokenizer Tokenizer,
        string InputIdsName,
        string AttentionMaskName,
        string? PositionIdsName,
        IReadOnlyList<PastKeyValueInput> PastKeyValues,
        string OutputName);

    public static Loaded Load(string modelDir, string outputNameHint)
    {
        var session = new InferenceSession(Path.Combine(modelDir, "model.onnx"));
        var tokenizer = LoadTokenizer(modelDir);

        var inputIdsName = session.InputMetadata.Keys.FirstOrDefault(n => n.Equals("input_ids", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("El modelo ONNX no expone una entrada 'input_ids'.");
        var attentionMaskName = session.InputMetadata.Keys.FirstOrDefault(n => n.Equals("attention_mask", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("El modelo ONNX no expone una entrada 'attention_mask'.");
        // Algunos exports (ej. DistilGPT-2) requieren position_ids explícito para el embedding
        // posicional; otros (ej. RoBERTa-detector) lo calculan internamente. Opcional a propósito.
        var positionIdsName = session.InputMetadata.Keys.FirstOrDefault(n => n.Equals("position_ids", StringComparison.OrdinalIgnoreCase));

        // Exports "with-past" (ej. DistilGPT-2) exigen tensores past_key_values.{capa}.{key,value}
        // incluso en el primer forward pass; se satisfacen con tensores vacíos (dim de secuencia=0).
        // Forma real observada: [batch, numHeads, seq, headDim], con numHeads/headDim estáticos.
        var pastKeyValues = session.InputMetadata
            .Where(kv => kv.Key.Contains("past_key_values", StringComparison.OrdinalIgnoreCase))
            .Select(kv => new PastKeyValueInput(kv.Key, kv.Value.Dimensions[1], kv.Value.Dimensions[3]))
            .ToList();

        var outputName = session.OutputMetadata.Keys.FirstOrDefault(n => n.Contains(outputNameHint, StringComparison.OrdinalIgnoreCase))
            ?? session.OutputMetadata.Keys.First();

        return new Loaded(session, tokenizer, inputIdsName, attentionMaskName, positionIdsName, pastKeyValues, outputName);
    }

    private static BpeTokenizer LoadTokenizer(string modelDir)
    {
        // Se parsea vocab.json/merges.txt a mano (en vez de usar el constructor de BpeOptions que
        // recibe rutas de archivo) porque ese constructor deserializa el JSON por reflexión
        // internamente, lo cual lanza InvalidOperationException bajo Native AOT (reflexión
        // deshabilitada). JsonSerializer.Deserialize aquí usa el contexto source-generated.
        var vocabJson = File.ReadAllText(Path.Combine(modelDir, "vocab.json"));
        var vocabulary = JsonSerializer.Deserialize<Dictionary<string, int>>(vocabJson, DetectionJson.Options)
            ?? throw new InvalidOperationException("vocab.json vacío o inválido.");
        var merges = File.ReadAllLines(Path.Combine(modelDir, "merges.txt"))
            .Where(line => line.Length > 0 && !line.StartsWith("#version", StringComparison.Ordinal));

        // ByteLevel=true es imprescindible: sin él, BpeTokenizer no antepone el marcador de
        // espacio byte-level ('Ġ') a las palabras que siguen a un espacio y tokeniza distinto a
        // como fue entrenado el modelo (confirmado: "world" da id 8331 en vez del correcto 232
        // de "Ġworld"). RobertaPreTokenizer aporta el regex de segmentación GPT-2/RoBERTa.
        return BpeTokenizer.Create(new BpeOptions(vocabulary)
        {
            Merges = merges,
            PreTokenizer = RobertaPreTokenizer.Instance,
            ByteLevel = true,
        });
    }
}
