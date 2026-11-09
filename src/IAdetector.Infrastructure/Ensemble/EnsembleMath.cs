using IAdetector.Core.Domain;

namespace IAdetector.Infrastructure.Ensemble;

/// <summary>Cálculos compartidos por las implementaciones de <see cref="IAdetector.Core.Abstractions.IEnsembleScorer"/>.</summary>
internal static class EnsembleMath
{
    // Dispersión (desviación estándar) máxima esperada entre señales en desacuerdo total; usada
    // para normalizar la confianza a [0,1].
    private const double MaxExpectedStdev = 0.5;

    /// <summary>
    /// Confianza = qué tan de acuerdo están las señales entre sí (1 - dispersión ponderada
    /// respecto a la media ponderada de las propias señales). La dispersión se mide contra
    /// esa media, no contra la probabilidad final: la sigmoide es no lineal, así que medir
    /// contra ella castigaba la confianza precisamente cuando las señales más coincidían
    /// en un extremo (todas en 0.85 dan P≈0.99 y parecían «dispersas» respecto a 0.99).
    /// </summary>
    public static double ComputeConfidence(IReadOnlyList<SignalScore> scores, double totalWeight)
    {
        if (totalWeight <= 0)
        {
            return 0.0;
        }

        var weightedMean = scores.Sum(s => s.Weight * s.NormalizedScore) / totalWeight;
        var variance = scores.Sum(s => s.Weight * Math.Pow(s.NormalizedScore - weightedMean, 2)) / totalWeight;
        var stdev = Math.Sqrt(variance);
        return Math.Clamp(1.0 - (stdev / MaxExpectedStdev), 0.0, 1.0);
    }

    public static Verdict DetermineVerdict(double aiProbability) => aiProbability switch
    {
        < 0.2 => Verdict.Human,
        < 0.4 => Verdict.LikelyHuman,
        < 0.6 => Verdict.Mixed,
        < 0.8 => Verdict.LikelyAI,
        _ => Verdict.AI,
    };
}
