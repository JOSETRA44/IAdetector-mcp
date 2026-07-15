using System.Text.Json.Serialization;
using IAdetector.Core.Domain;
using IAdetector.Core.UseCases;

namespace IAdetector.Infrastructure.Serialization;

/// <summary>Respuesta para <c>batch_detect_directory</c>/<c>batch</c> cuando no hay archivos que coincidan con el patrón.</summary>
public sealed record NoFilesFoundMessage(string Message, string Path, string Pattern);

/// <summary>
/// Contexto de serialización JSON generado en tiempo de compilación (sin reflexión), requerido
/// para que la publicación Native AOT (Fase 6) funcione: <c>JsonSerializer</c> con resolución por
/// reflexión lanza <see cref="InvalidOperationException"/> en binarios AOT/trimmed. También cubre
/// el parseo de <c>vocab.json</c> del tokenizador (ver <see cref="Ml.RobertaOnnxClassifier"/>),
/// que de otro modo dependería de la deserialización por reflexión interna de
/// <c>Microsoft.ML.Tokenizers.BpeOptions</c>.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true, Converters = [typeof(JsonStringEnumConverter<Verdict>)])]
[JsonSerializable(typeof(DetectionResult))]
[JsonSerializable(typeof(List<BatchDetectionEntry>))]
[JsonSerializable(typeof(NoFilesFoundMessage))]
[JsonSerializable(typeof(Dictionary<string, int>))]
internal partial class IAdetectorJsonContext : JsonSerializerContext;
