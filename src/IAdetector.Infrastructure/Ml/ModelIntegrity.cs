using System.Security.Cryptography;

namespace IAdetector.Infrastructure.Ml;

/// <summary>Estado de un archivo de modelo frente a su hash del catálogo.</summary>
public enum ModelFileState
{
    Ok,
    Missing,
    Corrupt,
}

/// <summary>Resultado de verificar un archivo: su estado y el hash real si se pudo calcular.</summary>
public sealed record ModelFileIntegrity(string FileName, ModelFileState State, string? ActualSha256);

/// <summary>Resultado de verificar todos los archivos de un modelo.</summary>
public sealed record ModelIntegrityReport(string ModelId, IReadOnlyList<ModelFileIntegrity> Files)
{
    /// <summary>True si todos los archivos del catálogo existen y su SHA-256 coincide.</summary>
    public bool IsIntact => Files.All(f => f.State == ModelFileState.Ok);
}

/// <summary>
/// Verificación de integridad de los modelos descargados contra los SHA-256 del
/// <see cref="ModelCatalog"/> (Fase 4). Descargar ya verificaba el hash en el momento;
/// esto cubre el después: un archivo truncado por un corte, corrupto en disco o
/// sustituido a mano dejaba de ser detectable — <see cref="FileSystemModelCache.IsDownloaded"/>
/// solo comprueba que el archivo exista, y el fallo aparecía luego como un error críptico
/// de ONNX al cargar. Lo usan el descargador (autorreparación) y `iadetector models verify`.
/// </summary>
public static class ModelIntegrity
{
    /// <summary>Verifica todos los archivos catalogados de un modelo dentro de <paramref name="modelDir"/>.</summary>
    public static async Task<ModelIntegrityReport> VerifyAsync(
        string modelId, string modelDir, CancellationToken cancellationToken = default)
    {
        var entry = ModelCatalog.Get(modelId);
        var files = new List<ModelFileIntegrity>(entry.Files.Count);

        foreach (var file in entry.Files)
        {
            var path = Path.Combine(modelDir, file.FileName);
            if (!File.Exists(path))
            {
                files.Add(new ModelFileIntegrity(file.FileName, ModelFileState.Missing, null));
                continue;
            }

            var actual = await ComputeSha256Async(path, cancellationToken).ConfigureAwait(false);
            var ok = string.Equals(actual, file.Sha256, StringComparison.OrdinalIgnoreCase);
            files.Add(new ModelFileIntegrity(file.FileName, ok ? ModelFileState.Ok : ModelFileState.Corrupt, actual));
        }

        return new ModelIntegrityReport(modelId, files);
    }

    /// <summary>True si el archivo existe y su SHA-256 coincide con el esperado.</summary>
    public static async Task<bool> VerifyFileAsync(
        string path, string expectedSha256, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        var actual = await ComputeSha256Async(path, cancellationToken).ConfigureAwait(false);
        return string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase);
    }

    internal static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }
}
