using IAdetector.Core.Abstractions;
using IAdetector.Core.Domain;

namespace IAdetector.Core.UseCases;

/// <summary>
/// Orquesta todas las <see cref="ISignalAnalyzer"/> registradas sobre un texto y combina
/// sus resultados vía <see cref="IEnsembleScorer"/>. Los textos largos se fragmentan en
/// ventanas solapadas para mantener el análisis representativo y acotar el costo por fragmento;
/// el resultado final agrega las señales de todos los fragmentos antes de aplicar el ensemble.
/// </summary>
public sealed class DetectTextUseCase(IEnumerable<ISignalAnalyzer> analyzers, IEnsembleScorer scorer)
{
    private const int ChunkWordThreshold = 2_500;
    private const int ChunkWords = 2_000;
    private const int ChunkOverlapWords = 200;

    // Acota el peor caso: sin esto, un archivo de varios MB (detect_file permite hasta 5MB) podría
    // generar cientos de fragmentos procesados secuencialmente, cada uno corriendo 2 modelos ONNX
    // — minutos de latencia para una sola detección. Por encima de este límite se muestrea
    // uniformemente en vez de analizar cada fragmento.
    private const int MaxChunks = 20;

    private readonly IReadOnlyList<ISignalAnalyzer> _analyzers = analyzers.ToList();

    public async Task<DetectionResult> DetectAsync(string text, CancellationToken cancellationToken = default)
    {
        var sample = TextSample.Create(text);

        return sample.WordCount <= ChunkWordThreshold
            ? await AnalyzeSingleAsync(sample, cancellationToken).ConfigureAwait(false)
            : await AnalyzeChunkedAsync(sample, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DetectionResult> AnalyzeSingleAsync(TextSample sample, CancellationToken cancellationToken)
    {
        var scores = await RunAnalyzersAsync(sample, cancellationToken).ConfigureAwait(false);
        return scorer.Combine(sample, scores);
    }

    private async Task<DetectionResult> AnalyzeChunkedAsync(TextSample fullSample, CancellationToken cancellationToken)
    {
        var allChunks = BuildChunks(fullSample.Words);
        var sampled = allChunks.Count > MaxChunks;
        var chunksToAnalyze = sampled ? SampleEvenly(allChunks, MaxChunks) : allChunks;

        var perChunkScores = new List<(int WordCount, IReadOnlyList<SignalScore> Scores)>(chunksToAnalyze.Count);

        foreach (var chunkWords in chunksToAnalyze)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chunkSample = TextSample.Create(string.Join(' ', chunkWords));
            var scores = await RunAnalyzersAsync(chunkSample, cancellationToken).ConfigureAwait(false);
            perChunkScores.Add((chunkWords.Count, scores));
        }

        var aggregated = AggregateSignals(perChunkScores);
        var chunkInfo = new ChunkInfo(chunksToAnalyze.Count, fullSample.WordCount, sampled);
        return scorer.Combine(fullSample, aggregated, chunkInfo);
    }

    private static List<List<string>> SampleEvenly(List<List<string>> chunks, int count)
    {
        // Selecciona `count` fragmentos uniformemente espaciados a lo largo de todo el documento
        // (no solo el principio), para que la muestra siga siendo representativa del texto completo.
        var step = chunks.Count / (double)count;
        var result = new List<List<string>>(count);
        for (var i = 0; i < count; i++)
        {
            var index = Math.Min((int)(i * step), chunks.Count - 1);
            result.Add(chunks[index]);
        }

        return result;
    }

    private async Task<IReadOnlyList<SignalScore>> RunAnalyzersAsync(TextSample sample, CancellationToken cancellationToken)
    {
        var tasks = _analyzers.Select(a => a.AnalyzeAsync(sample, cancellationToken));
        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static List<List<string>> BuildChunks(IReadOnlyList<string> words)
    {
        var chunks = new List<List<string>>();
        var step = ChunkWords - ChunkOverlapWords;

        for (var start = 0; start < words.Count; start += step)
        {
            var length = Math.Min(ChunkWords, words.Count - start);
            chunks.Add([.. words.Skip(start).Take(length)]);
            if (start + length >= words.Count)
            {
                break;
            }
        }

        return chunks;
    }

    private static IReadOnlyList<SignalScore> AggregateSignals(
        List<(int WordCount, IReadOnlyList<SignalScore> Scores)> perChunk)
    {
        var totalWords = perChunk.Sum(c => c.WordCount);

        return perChunk
            .SelectMany(chunk => chunk.Scores.Select(score => (chunk.WordCount, Score: score)))
            .GroupBy(x => x.Score.Name)
            .Select(g =>
            {
                var weightedRaw = g.Sum(x => x.Score.RawValue * x.WordCount) / (double)totalWords;
                var weightedNorm = g.Sum(x => x.Score.NormalizedScore * x.WordCount) / (double)totalWords;
                return new SignalScore(
                    g.Key,
                    weightedRaw,
                    weightedNorm,
                    g.First().Score.Weight,
                    $"Promedio ponderado sobre {g.Count()} fragmentos.");
            })
            .ToList();
    }
}
