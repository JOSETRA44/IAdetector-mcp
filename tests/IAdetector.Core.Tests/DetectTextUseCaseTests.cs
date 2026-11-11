using IAdetector.Core.Domain;
using IAdetector.Core.Tests.Fakes;
using IAdetector.Core.UseCases;
using Xunit;

namespace IAdetector.Core.Tests;

public class DetectTextUseCaseTests
{
    [Fact]
    public async Task DetectAsync_ShortText_RunsAnalyzersOnceAndSkipsChunking()
    {
        var analyzerA = new RecordingAnalyzer("a", s => new SignalScore("a", 0.2, 0.2, 1.0, "a"));
        var analyzerB = new RecordingAnalyzer("b", s => new SignalScore("b", 0.8, 0.8, 1.0, "b"));
        var scorer = new RecordingScorer();
        var useCase = new DetectTextUseCase([analyzerA, analyzerB], scorer);

        var text = string.Join(' ', Enumerable.Repeat("word", 50));
        var result = await useCase.DetectAsync(text);

        Assert.Equal(1, scorer.CallCount);
        Assert.Null(scorer.LastChunkInfo);
        Assert.Null(result.ChunkInfo);
        Assert.Equal(50, scorer.LastSample!.WordCount);
        Assert.Equal(2, scorer.LastScores!.Count);
        Assert.Contains(scorer.LastScores, s => s.Name == "a" && s.NormalizedScore == 0.2);
        Assert.Contains(scorer.LastScores, s => s.Name == "b" && s.NormalizedScore == 0.8);
    }

    [Fact]
    public async Task DetectAsync_LongText_SplitsIntoOverlappingChunksAndWeightsBySize()
    {
        // Analiza según el tamaño del propio fragmento: fragmentos grandes (>=1000 palabras)
        // puntúan 1.0, fragmentos pequeños puntúan 0.0. Con 4000 palabras, el chunking
        // (ventanas de 2000, paso 1800) produce fragmentos de 2000, 2000 y 400 palabras.
        var analyzer = new RecordingAnalyzer(
            "size-dependent",
            s => new SignalScore("size-dependent", s.WordCount, s.WordCount >= 1000 ? 1.0 : 0.0, 1.0, "x"));
        var scorer = new RecordingScorer();
        var useCase = new DetectTextUseCase([analyzer], scorer);

        var text = string.Join(' ', Enumerable.Repeat("word", 4000));
        var result = await useCase.DetectAsync(text);

        Assert.Equal(1, scorer.CallCount);
        Assert.NotNull(result.ChunkInfo);
        Assert.Equal(3, result.ChunkInfo!.TotalChunks);
        Assert.Equal(4000, result.ChunkInfo!.TotalWords);

        var aggregated = Assert.Single(scorer.LastScores!);
        // (1.0*2000 + 1.0*2000 + 0.0*400) / (2000+2000+400)
        Assert.Equal(4000.0 / 4400.0, aggregated.NormalizedScore, precision: 6);
    }

    [Fact]
    public async Task DetectAsync_VeryLongText_SamplesInsteadOfAnalyzingEveryChunk()
    {
        // Un documento de 50 000 palabras generaría ~28 fragmentos naturales (ventanas de 2000,
        // paso 1800) — muy por encima del límite de 20, así que debe muestrear en vez de analizar
        // cada uno (evita minutos de latencia para un archivo grande en detect_file).
        var analyzer = new RecordingAnalyzer("constant", s => new SignalScore("constant", 0.7, 0.7, 1.0, "x"));
        var scorer = new RecordingScorer();
        var useCase = new DetectTextUseCase([analyzer], scorer);

        var text = string.Join(' ', Enumerable.Repeat("word", 50_000));
        var result = await useCase.DetectAsync(text);

        Assert.NotNull(result.ChunkInfo);
        Assert.True(result.ChunkInfo!.Sampled);
        Assert.Equal(20, result.ChunkInfo!.TotalChunks);
        Assert.Equal(50_000, result.ChunkInfo!.TotalWords);

        var aggregated = Assert.Single(scorer.LastScores!);
        Assert.Equal(0.7, aggregated.NormalizedScore, precision: 6);
    }

    [Fact]
    public async Task DetectAsync_NoAnalyzers_StillCallsScorerWithEmptyList()
    {
        var scorer = new RecordingScorer();
        var useCase = new DetectTextUseCase([], scorer);

        await useCase.DetectAsync("some short text here");

        Assert.Equal(1, scorer.CallCount);
        Assert.Empty(scorer.LastScores!);
    }
}

public class DetectTextUseCaseParallelismTests
{
    private sealed class SlowProbeAnalyzer : IAdetector.Core.Abstractions.ISignalAnalyzer
    {
        private int _current;
        private int _max;
        public string Name => "slow-probe";
        public int MaxConcurrent => Volatile.Read(ref _max);

        public async Task<SignalScore> AnalyzeAsync(TextSample sample, CancellationToken cancellationToken = default)
        {
            var now = Interlocked.Increment(ref _current);
            int observed;
            do
            {
                observed = Volatile.Read(ref _max);
            }
            while (now > observed && Interlocked.CompareExchange(ref _max, now, observed) != observed);

            try
            {
                await Task.Delay(100, cancellationToken);
                return new SignalScore(Name, 0.5, 0.5, 1.0, "probe");
            }
            finally
            {
                Interlocked.Decrement(ref _current);
            }
        }
    }

    [Fact]
    public async Task DetectAsync_LongText_AnalyzesChunksConcurrently()
    {
        if (Environment.ProcessorCount < 2)
        {
            return; // Sin paralelismo posible en un solo núcleo: nada que afirmar.
        }

        var analyzer = new SlowProbeAnalyzer();
        var useCase = new DetectTextUseCase([analyzer], new RecordingScorer());
        var text = string.Join(' ', Enumerable.Repeat("word", 4000));

        var result = await useCase.DetectAsync(text);

        Assert.Equal(3, result.ChunkInfo!.TotalChunks);
        Assert.True(analyzer.MaxConcurrent >= 2,
            $"Se esperaba solape entre fragmentos y el máximo observado fue {analyzer.MaxConcurrent}.");
    }
}
