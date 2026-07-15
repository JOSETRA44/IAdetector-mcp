using IAdetector.Core.Abstractions;

namespace IAdetector.Infrastructure.Ml;

/// <summary>Cachea los modelos descargados en <c>%LOCALAPPDATA%\IAdetector\models\&lt;modelId&gt;\</c>.</summary>
public sealed class FileSystemModelCache : IModelCache
{
    private static readonly string RootDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IAdetector", "models");

    public bool IsDownloaded(string modelId)
    {
        var entry = ModelCatalog.Get(modelId);
        var dir = ModelDir(modelId);
        return entry.Files.All(f => File.Exists(Path.Combine(dir, f.FileName)));
    }

    public string GetPath(string modelId) => ModelDir(modelId);

    internal static string ModelDir(string modelId) => Path.Combine(RootDir, modelId);
}
