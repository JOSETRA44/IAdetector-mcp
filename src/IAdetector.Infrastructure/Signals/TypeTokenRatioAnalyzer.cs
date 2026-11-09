using IAdetector.Core.Abstractions;
using IAdetector.Core.Domain;

namespace IAdetector.Infrastructure.Signals;

/// <summary>
/// Type-Token Ratio: proporción de palabras únicas sobre el total. Un TTR bajo indica
/// vocabulario repetitivo, más típico (aunque no exclusivo) de texto generado por IA.
/// El TTR crudo decae con la longitud del texto (ley de Heaps), así que exige un mínimo de
/// palabras alto para no confundir "texto corto" con "vocabulario repetitivo".
/// </summary>
public sealed class TypeTokenRatioAnalyzer : ISignalAnalyzer
{
    public string Name => "type-token-ratio";

    private const int MinWords = 400;
    private const double Weight = 0.8;

    public Task<SignalScore> AnalyzeAsync(TextSample sample, CancellationToken cancellationToken = default)
    {
        if (sample.WordCount < MinWords)
        {
            return Task.FromResult(new SignalScore(
                Name, 0.0, 0.5, Weight * ScoreNormalization.InsufficientDataWeightFactor,
                $"Muy pocas palabras ({sample.WordCount}) para un TTR confiable.", HasData: false));
        }

        var distinctWords = sample.Words
            .Select(w => w.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Count();

        var ttr = distinctWords / (double)sample.WordCount;
        var score = ScoreNormalization.MapToProbability(ttr, valueAtZero: 0.55, valueAtOne: 0.25);

        return Task.FromResult(new SignalScore(
            Name, ttr, score, Weight,
            $"TTR={ttr:F2} ({distinctWords} palabras únicas de {sample.WordCount})."));
    }
}
