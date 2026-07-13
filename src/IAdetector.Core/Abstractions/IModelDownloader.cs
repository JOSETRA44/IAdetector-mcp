namespace IAdetector.Core.Abstractions;

/// <summary>
/// Descarga (bajo demanda, nunca automáticamente durante una detección) los modelos ONNX
/// catalogados. Ver comando CLI <c>iadetector models download</c> y herramientas MCP.
/// </summary>
public interface IModelDownloader
{
    Task<string> EnsureAvailableAsync(
        string modelId,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
