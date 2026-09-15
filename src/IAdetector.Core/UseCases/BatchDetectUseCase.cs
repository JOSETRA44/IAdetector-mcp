using System.Threading.Channels;
using IAdetector.Core.Abstractions;
using IAdetector.Core.Domain;

namespace IAdetector.Core.UseCases;

/// <summary>Resultado de analizar un archivo dentro de un lote: <see cref="Error"/> es no-nulo si el archivo no pudo leerse.</summary>
public sealed record BatchDetectionEntry(string Path, DetectionResult? Result, string? Error)
{
    /// <summary>True si este resultado salió de la caché en vez de recalcularse.</summary>
    public bool Cached { get; init; }

    /// <summary>Palabras del archivo analizado; 0 cuando no pudo leerse.</summary>
    public int TotalWords { get; init; }
}

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
                results[i] = await DetectFileAsync(paths[i], DetectionCacheMode.ReadWrite, ct).ConfigureAwait(false);
            }).ConfigureAwait(false);

        return results;
    }

    /// <summary>
    /// Variante en streaming: emite cada entrada en cuanto termina, en vez de esperar a que el
    /// corpus completo esté analizado. Es lo que permite que <c>batch -f jsonl</c> vaya escribiendo
    /// líneas mientras corre — con 500 documentos, bufferizar todo significa no ver nada durante minutos.
    /// El orden de emisión es de finalización, no de entrada (con paralelismo &gt; 1 no hay orden estable).
    /// </summary>
    public async IAsyncEnumerable<BatchDetectionEntry> DetectFilesStreamingAsync(
        IEnumerable<string> filePaths,
        int maxDegreeOfParallelism = 4,
        DetectionCacheMode cacheMode = DetectionCacheMode.ReadWrite,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<BatchDetectionEntry>(new UnboundedChannelOptions
        {
            SingleReader = true,
        });

        var producer = Task.Run(
            async () =>
            {
                try
                {
                    await Parallel.ForEachAsync(
                        filePaths,
                        new ParallelOptions
                        {
                            MaxDegreeOfParallelism = maxDegreeOfParallelism,
                            CancellationToken = cancellationToken,
                        },
                        async (path, ct) =>
                        {
                            var entry = await DetectFileAsync(path, cacheMode, ct).ConfigureAwait(false);
                            await channel.Writer.WriteAsync(entry, ct).ConfigureAwait(false);
                        }).ConfigureAwait(false);

                    channel.Writer.Complete();
                }
                catch (Exception ex)
                {
                    channel.Writer.Complete(ex);
                }
            },
            CancellationToken.None);

        await foreach (var entry in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return entry;
        }

        await producer.ConfigureAwait(false);
    }

    private async Task<BatchDetectionEntry> DetectFileAsync(
        string path,
        DetectionCacheMode cacheMode,
        CancellationToken cancellationToken)
    {
        try
        {
            var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            var outcome = await detectText.DetectWithCacheAsync(text, cacheMode, cancellationToken).ConfigureAwait(false);
            return new BatchDetectionEntry(path, outcome.Result, null)
            {
                Cached = outcome.Cached,
                TotalWords = outcome.TotalWords,
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new BatchDetectionEntry(path, null, ex.Message);
        }
    }
}
