using IAdetector.Core.Domain;

namespace IAdetector.Core.UseCases;

/// <summary>Resultado de analizar un archivo dentro de un lote: <see cref="Error"/> es no-nulo si el archivo no pudo leerse.</summary>
public sealed record BatchDetectionEntry(string Path, DetectionResult? Result, string? Error);

/// <summary>
/// Analiza varios archivos de texto en paralelo (acotado), reutilizando <see cref="DetectTextUseCase"/>.
/// Un archivo ilegible no aborta el lote completo: se reporta como entrada con <see cref="BatchDetectionEntry.Error"/>.
/// </summary>
public sealed class BatchDetectUseCase(DetectTextUseCase detectText)
{
    public async Task<IReadOnlyList<BatchDetectionEntry>> DetectFilesAsync(
        IEnumerable<string> filePaths,
        int maxDegreeOfParallelism = 4,
        CancellationToken cancellationToken = default)
    {
        var paths = filePaths.ToList();
        var results = new BatchDetectionEntry[paths.Count];

        await Parallel.ForEachAsync(
            Enumerable.Range(0, paths.Count),
            new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism, CancellationToken = cancellationToken },
            async (i, ct) =>
            {
                var path = paths[i];
                try
                {
                    var text = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
                    var result = await detectText.DetectAsync(text, ct).ConfigureAwait(false);
                    results[i] = new BatchDetectionEntry(path, result, null);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    results[i] = new BatchDetectionEntry(path, null, ex.Message);
                }
            }).ConfigureAwait(false);

        return results;
    }
}
