using System.Text.Json;

namespace IAdetector.Infrastructure.Serialization;

/// <summary>
/// Opciones de serialización compartidas entre el servidor MCP y la CLI, para una salida JSON
/// consistente. Respaldadas por <see cref="IAdetectorJsonContext"/> (source-generated, sin
/// reflexión) para ser compatible con Native AOT.
/// </summary>
public static class DetectionJson
{
    public static readonly JsonSerializerOptions Options = IAdetectorJsonContext.Default.Options;
}
