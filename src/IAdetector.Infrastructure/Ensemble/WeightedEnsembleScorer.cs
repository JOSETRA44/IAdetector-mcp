using IAdetector.Core.Abstractions;
using IAdetector.Core.Domain;

namespace IAdetector.Infrastructure.Ensemble;

/// <summary>
/// Combina señales mediante un promedio ponderado fijo. Pesos iniciales definidos junto a cada
/// analizador (Fase 1); la Fase 3 los reemplaza por coeficientes calibrados offline
/// (<c>EnsembleWeights.json</c>) sin cambiar este contrato.
/// </summary>
public sealed class WeightedEnsembleScorer : IEnsembleScorer
{
    // Dispersión (desviación estándar) máxima esperada entre señales en desacuerdo total; usada
    // para normalizar la confianza a [0,1].
    private const double MaxExpectedStdev = 0.5;

    public DetectionResult Combine(TextSample sample, IReadOnlyList<SignalScore> scores, ChunkInfo? chunkInfo = null)
    {
        if (scores.Count == 0)
        {
            return new DetectionResult(Verdict.Mixed, 0.5, 0.0, scores, chunkInfo);
        }

        var totalWeight = scores.Sum(s => s.Weight);
        var aiProbability = totalWeight > 0
            ? scores.Sum(s => s.NormalizedScore * s.Weight) / totalWeight
            : scores.Average(s => s.NormalizedScore);

        var confidence = ComputeConfidence(scores, totalWeight, aiProbability);
        var verdict = DetermineVerdict(aiProbability);

        return new DetectionResult(verdict, aiProbability, confidence, scores, chunkInfo);
    }

    private static double ComputeConfidence(IReadOnlyList<SignalScore> scores, double totalWeight, double aiProbability)
    {
        if (totalWeight <= 0)
        {
            return 0.0;
        }

        var variance = scores.Sum(s => s.Weight * Math.Pow(s.NormalizedScore - aiProbability, 2)) / totalWeight;
        var stdev = Math.Sqrt(variance);
        return Math.Clamp(1.0 - (stdev / MaxExpectedStdev), 0.0, 1.0);
    }

    private static Verdict DetermineVerdict(double aiProbability) => aiProbability switch
    {
        < 0.2 => Verdict.Human,
        < 0.4 => Verdict.LikelyHuman,
        < 0.6 => Verdict.Mixed,
        < 0.8 => Verdict.LikelyAI,
        _ => Verdict.AI,
    };
}
