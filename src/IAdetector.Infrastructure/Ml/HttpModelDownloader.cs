using IAdetector.Core.Abstractions;

namespace IAdetector.Infrastructure.Ml;

/// <summary>
/// Descarga los archivos de un modelo del <see cref="ModelCatalog"/> a disco, verificando su
/// SHA-256 tras cada descarga. Idempotente: si un archivo ya existe Y su hash coincide, no se
/// vuelve a descargar (permite reintentar tras una descarga parcial sin repetir lo ya
/// obtenido); si existe pero está corrupto, se borra y se descarga de nuevo (Fase 4):
/// antes, un archivo dañado se daba por bueno para siempre solo por existir.
/// </summary>
public sealed class HttpModelDownloader : IModelDownloader, IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(15) };

    public async Task<string> EnsureAvailableAsync(
        string modelId,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var entry = ModelCatalog.Get(modelId);
        var dir = FileSystemModelCache.ModelDir(modelId);
        Directory.CreateDirectory(dir);

        var totalBytes = entry.Files.Sum(f => f.ApproxSizeBytes);
        long bytesDoneBeforeCurrentFile = 0;

        foreach (var file in entry.Files)
        {
            var destPath = Path.Combine(dir, file.FileName);
            if (File.Exists(destPath)
                && !await ModelIntegrity.VerifyFileAsync(destPath, file.Sha256, cancellationToken).ConfigureAwait(false))
            {
                File.Delete(destPath);
            }

            if (!File.Exists(destPath))
            {
                await DownloadOneAsync(file, destPath, progress, totalBytes, bytesDoneBeforeCurrentFile, cancellationToken)
                    .ConfigureAwait(false);
            }

            bytesDoneBeforeCurrentFile += file.ApproxSizeBytes;
        }

        progress?.Report(1.0);
        return dir;
    }

    private async Task DownloadOneAsync(
        ModelFile file,
        string destPath,
        IProgress<double>? progress,
        long totalBytes,
        long bytesDoneBeforeCurrentFile,
        CancellationToken cancellationToken)
    {
        var tmpPath = destPath + ".download";

        using (var response = await _http
            .GetAsync(file.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();

            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var output = File.Create(tmpPath);

            var buffer = new byte[81_920];
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                if (progress is not null && totalBytes > 0)
                {
                    var done = bytesDoneBeforeCurrentFile + output.Position;
                    progress.Report(Math.Min(done / (double)totalBytes, 0.999));
                }
            }
        }

        var actualHash = await ModelIntegrity.ComputeSha256Async(tmpPath, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(actualHash, file.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(tmpPath);
            throw new InvalidOperationException(
                $"Verificación de integridad fallida para '{file.FileName}': se esperaba SHA-256 {file.Sha256}, se obtuvo {actualHash}.");
        }

        File.Move(tmpPath, destPath, overwrite: true);
    }

    public void Dispose() => _http.Dispose();
}
