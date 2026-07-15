namespace IAdetector.Core.Abstractions;

/// <summary>Consulta si un modelo ONNX ya fue descargado localmente y dónde está.</summary>
public interface IModelCache
{
    bool IsDownloaded(string modelId);

    /// <summary>Directorio local que contiene los archivos del modelo (pesos + tokenizador). Solo válido si <see cref="IsDownloaded"/> es true.</summary>
    string GetPath(string modelId);
}
