using System.Text.Json.Serialization;
using IAdetector.Core.Domain;
using IAdetector.Core.UseCases;
using IAdetector.Infrastructure.Ensemble;
using HumanReferenceRangesFile = IAdetector.Infrastructure.Ensemble.HumanReferenceRangesFile;

namespace IAdetector.Infrastructure.Serialization;

/// <summary>Respuesta para <c>batch_detect_directory</c>/<c>batch</c> cuando no hay archivos que coincidan con el patrón.</summary>
public sealed record NoFilesFoundMessage(string Message, string Path, string Pattern);

/// <summary>
/// Una entrada de la caché de resultados en disco (ver <see cref="Caching.FileSystemDetectionCache"/>).
/// <paramref name="Version"/> se re-verifica al leer para descartar entradas de otra calibración.
/// </summary>
public sealed record CachedDetectionEntry(string Version, DateTimeOffset FetchedAt, DetectionResult Result);

/// <summary>
/// Contexto de serialización JSON generado en tiempo de compilación (sin reflexión), requerido
/// para que la publicación Native AOT (Fase 6) funcione: <c>JsonSerializer</c> con resolución por
/// reflexión lanza <see cref="InvalidOperationException"/> en binarios AOT/trimmed. También cubre
/// el parseo de <c>vocab.json</c> del tokenizador (ver <see cref="Ml.RobertaOnnxClassifier"/>),
/// que de otro modo dependería de la deserialización por reflexión interna de
/// <c>Microsoft.ML.Tokenizers.BpeOptions</c>.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    Converters = [typeof(JsonStringEnumConverter<Verdict>), typeof(JsonStringEnumConverter<TextLanguage>)])]
[JsonSerializable(typeof(DetectionResult))]
[JsonSerializable(typeof(DetectionExplanation))]
[JsonSerializable(typeof(VersionComparison))]
[JsonSerializable(typeof(HumanReferenceRangesFile))]
[JsonSerializable(typeof(List<BatchDetectionEntry>))]
[JsonSerializable(typeof(NoFilesFoundMessage))]
[JsonSerializable(typeof(CachedDetectionEntry))]
[JsonSerializable(typeof(Dictionary<string, int>))]
[JsonSerializable(typeof(Dictionary<string, double>))]
[JsonSerializable(typeof(EnsembleWeightsData))]
public partial class IAdetectorJsonContext : JsonSerializerContext;
