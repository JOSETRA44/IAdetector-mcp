namespace IAdetector.Infrastructure.Ml;

/// <summary>Un archivo descargable perteneciente a un modelo (pesos ONNX o archivos de tokenizador).</summary>
/// <param name="FileName">Nombre local con el que se guarda (estable, independiente del nombre de origen en HuggingFace).</param>
/// <param name="Url">URL de descarga directa.</param>
/// <param name="Sha256">Hash SHA-256 esperado, verificado tras la descarga.</param>
/// <param name="ApproxSizeBytes">Tamaño aproximado, usado solo para reportar progreso si el servidor no envía Content-Length.</param>
public sealed record ModelFile(string FileName, Uri Url, string Sha256, long ApproxSizeBytes);

public sealed record ModelCatalogEntry(string ModelId, string DisplayName, IReadOnlyList<ModelFile> Files);

/// <summary>Catálogo estático de modelos ONNX descargables bajo demanda (nunca automáticamente durante una detección).</summary>
public static class ModelCatalog
{
    public const string RobertaOpenAiDetector = "roberta-openai-detector";
    public const string DistilGpt2Perplexity = "distilgpt2-perplexity";

    private const string RobertaRepoBaseUrl = "https://huggingface.co/onnx-community/roberta-base-openai-detector-ONNX/resolve/main";
    private const string DistilGpt2RepoBaseUrl = "https://huggingface.co/onnx-community/distilgpt2-ONNX/resolve/main";

    public static readonly IReadOnlyDictionary<string, ModelCatalogEntry> Entries =
        new Dictionary<string, ModelCatalogEntry>
        {
            [RobertaOpenAiDetector] = new ModelCatalogEntry(
                RobertaOpenAiDetector,
                "RoBERTa OpenAI Detector (ONNX, cuantizado int8)",
                [
                    new ModelFile(
                        "model.onnx",
                        new Uri($"{RobertaRepoBaseUrl}/onnx/model_quantized.onnx"),
                        "d9c0412c0e1976fbee55b9a254b652b9293e79033c9737b22d85ecf1114697cf",
                        125_855_421),
                    new ModelFile(
                        "vocab.json",
                        new Uri($"{RobertaRepoBaseUrl}/vocab.json"),
                        "ed19656ea1707df69134c4af35c8ceda2cc9860bf2c3495026153a133670ab5e",
                        798_293),
                    new ModelFile(
                        "merges.txt",
                        new Uri($"{RobertaRepoBaseUrl}/merges.txt"),
                        "1ce1664773c50f3e0cc8842619a93edc4624525b728b188a9e0be33b7726adc5",
                        456_318),
                ]),

            [DistilGpt2Perplexity] = new ModelCatalogEntry(
                DistilGpt2Perplexity,
                "DistilGPT-2 (ONNX, cuantizado int8) — señal de perplexity",
                [
                    new ModelFile(
                        "model.onnx",
                        new Uri($"{DistilGpt2RepoBaseUrl}/onnx/model_quantized.onnx"),
                        "f1b302b9c052e6380787f7812adadc3756e3eea76770f84f0e6a78aed58a691c",
                        236_714_483),
                    new ModelFile(
                        "vocab.json",
                        new Uri($"{DistilGpt2RepoBaseUrl}/vocab.json"),
                        "3ba3c3109ff33976c4bd966589c11ee14fcaa1f4c9e5e154c2ed7f99d80709e7",
                        798_156),
                    // Mismas fusiones BPE que RoBERTa (GPT-2 y RoBERTa comparten el mismo esquema
                    // byte-level BPE) — mismo hash SHA-256, verificado al descargar ambos modelos.
                    new ModelFile(
                        "merges.txt",
                        new Uri($"{DistilGpt2RepoBaseUrl}/merges.txt"),
                        "1ce1664773c50f3e0cc8842619a93edc4624525b728b188a9e0be33b7726adc5",
                        456_318),
                ]),
        };

    public static ModelCatalogEntry Get(string modelId) =>
        Entries.TryGetValue(modelId, out var entry)
            ? entry
            : throw new KeyNotFoundException($"Modelo desconocido: '{modelId}'.");
}
