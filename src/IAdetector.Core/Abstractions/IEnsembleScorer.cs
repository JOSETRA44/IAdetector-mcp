using IAdetector.Core.Domain;

namespace IAdetector.Core.Abstractions;

/// <summary>Combina las señales individuales en un <see cref="DetectionResult"/> final.</summary>
public interface IEnsembleScorer
{
    DetectionResult Combine(TextSample sample, IReadOnlyList<SignalScore> scores, ChunkInfo? chunkInfo = null);
}
