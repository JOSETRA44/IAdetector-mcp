namespace IAdetector.Infrastructure.Ml;

/// <summary>Un archivo descargable perteneciente a un modelo (pesos ONNX o archivos de tokenizador).</summary>
/// <param name="FileName">Nombre local con el que se guarda (estable, independiente del nombre de origen en HuggingFace).</param>
/// <param name="Url">URL de descarga directa.</param>
/// <param name="Sha256">Hash SHA-256 esperado, verificado tras la descarga.</param>
/// <param name="ApproxSizeBytes">Tamaño aproximado, usado solo para reportar progreso si el servidor no envía Content-Length.</param>
public sealed record ModelFile(string FileName, Uri Url, string Sha256, long ApproxSizeBytes);

public sealed record ModelCatalogEntry(string ModelId, string DisplayName, IReadOnlyList<ModelFile> Files);

/// <summary>
/// Catálogo estático de modelos ONNX descargables bajo demanda. Un solo modelo por ahora
/// (Fase 2); la Fase 4 añadirá un modelo de perplexity con la misma infraestructura.
/// </summary>
public static class ModelCatalog
{
    public const string RobertaOpenAiDetector = "roberta-openai-detector";

    private const string RepoBaseUrl = "https://huggingface.co/onnx-community/roberta-base-openai-detector-ONNX/resolve/main";

    public static readonly IReadOnlyDictionary<string, ModelCatalogEntry> Entries =
        new Dictionary<string, ModelCatalogEntry>
        {
            [RobertaOpenAiDetector] = new ModelCatalogEntry(
                RobertaOpenAiDetector,
                "RoBERTa OpenAI Detector (ONNX, cuantizado int8)",
                [
                    new ModelFile(
                        "model.onnx",
                        new Uri($"{RepoBaseUrl}/onnx/model_quantized.onnx"),
                        "d9c0412c0e1976fbee55b9a254b652b9293e79033c9737b22d85ecf1114697cf",
                        125_855_421),
                    new ModelFile(
                        "vocab.json",
                        new Uri($"{RepoBaseUrl}/vocab.json"),
                        "ed19656ea1707df69134c4af35c8ceda2cc9860bf2c3495026153a133670ab5e",
                        798_293),
                    new ModelFile(
                        "merges.txt",
                        new Uri($"{RepoBaseUrl}/merges.txt"),
                        "1ce1664773c50f3e0cc8842619a93edc4624525b728b188a9e0be33b7726adc5",
                        456_318),
                ]),
        };

    public static ModelCatalogEntry Get(string modelId) =>
        Entries.TryGetValue(modelId, out var entry)
            ? entry
            : throw new KeyNotFoundException($"Modelo desconocido: '{modelId}'.");
}
