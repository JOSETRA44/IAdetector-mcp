using IAdetector.Core.Domain;
using IAdetector.Core.UseCases;

namespace IAdetector.Infrastructure.Serialization;

/// <summary>
/// Recorta el desglose de señales de un resultado cuando no se pidió modo verbose. Compartido
/// entre el servidor MCP y la CLI para que ambos apliquen exactamente el mismo criterio.
/// </summary>
public static class DetectionResultProjection
{
    public static DetectionResult Apply(DetectionResult result, bool verbose) =>
        verbose ? result : result with { Signals = [] };

    public static BatchDetectionEntry Apply(BatchDetectionEntry entry, bool verbose) =>
        entry.Result is null ? entry : entry with { Result = Apply(entry.Result, verbose) };
}
