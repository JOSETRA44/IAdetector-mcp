using IAdetector.Core.Abstractions;
using IAdetector.Core.Domain;

namespace IAdetector.Core.Tests.Fakes;

internal sealed class RecordingScorer : IEnsembleScorer
{
    public TextSample? LastSample { get; private set; }
    public IReadOnlyList<SignalScore>? LastScores { get; private set; }
    public ChunkInfo? LastChunkInfo { get; private set; }
    public int CallCount { get; private set; }

    public DetectionResult Combine(TextSample sample, IReadOnlyList<SignalScore> scores, ChunkInfo? chunkInfo = null)
    {
        CallCount++;
        LastSample = sample;
        LastScores = scores;
        LastChunkInfo = chunkInfo;

        var aiProbability = scores.Count == 0 ? 0.5 : scores.Average(s => s.NormalizedScore);
        return new DetectionResult(Verdict.Mixed, aiProbability, 1.0, scores, chunkInfo);
    }
}
