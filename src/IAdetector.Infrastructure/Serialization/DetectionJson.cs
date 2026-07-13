using System.Text.Json;
using System.Text.Json.Serialization;

namespace IAdetector.Infrastructure.Serialization;

/// <summary>Opciones de serialización compartidas entre el servidor MCP y la CLI, para una salida JSON consistente.</summary>
public static class DetectionJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };
}
