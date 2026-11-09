using IAdetector.Core.Abstractions;
using IAdetector.Core.Domain;

namespace IAdetector.Infrastructure.Signals;

/// <summary>
/// Entropía de Shannon de la distribución de palabras, normalizada por log2(vocabulario) para
/// obtener un valor relativo en [0,1] independiente del tamaño del texto. Una entropía relativa
/// baja indica una distribución de palabras más predecible/repetitiva.
/// </summary>
public sealed class EntropyAnalyzer : ISignalAnalyzer
{
    public string Name => "lexical-entropy";

    private const int MinWords = 400;
    private const double Weight = 0.8;

    public Task<SignalScore> AnalyzeAsync(TextSample sample, CancellationToken cancellationToken = default)
    {
        var frequencies = sample.Words
            .Select(w => w.ToLowerInvariant())
            .GroupBy(w => w, StringComparer.Ordinal)
            .Select(g => g.Count())
            .ToList();

        if (sample.WordCount < MinWords || frequencies.Count <= 1)
        {
            return Task.FromResult(new SignalScore(
                Name, 0.0, 0.5, Weight * ScoreNormalization.InsufficientDataWeightFactor,
                $"Muy pocas palabras o vocabulario degenerado ({sample.WordCount} palabras, {frequencies.Count} distintas).", HasData: false));
        }

        var total = (double)sample.WordCount;
        var entropy = -frequencies.Sum(count =>
        {
            var p = count / total;
            return p * Math.Log2(p);
        });

        var maxEntropy = Math.Log2(frequencies.Count);
        var relativeEntropy = entropy / maxEntropy;
        var score = ScoreNormalization.MapToProbability(relativeEntropy, valueAtZero: 0.9, valueAtOne: 0.6);

        return Task.FromResult(new SignalScore(
            Name, relativeEntropy, score, Weight,
            $"Entropía relativa={relativeEntropy:F2} sobre {frequencies.Count} palabras distintas."));
    }
}
