using IAdetector.Core.Abstractions;
using IAdetector.Core.Domain;

namespace IAdetector.Infrastructure.Signals;

/// <summary>
/// Proporción de trigramas de palabras que se repiten dentro del texto. Una repetición alta de
/// n-gramas es una señal débil pero barata de generación mecánica/degenerada.
/// </summary>
public sealed class NGramRepetitionAnalyzer : ISignalAnalyzer
{
    public string Name => "ngram-repetition";

    private const int MinWords = 200;
    private const int N = 3;
    private const double Weight = 0.8;

    public Task<SignalScore> AnalyzeAsync(TextSample sample, CancellationToken cancellationToken = default)
    {
        if (sample.WordCount < MinWords)
        {
            return Task.FromResult(new SignalScore(
                Name, 0.0, 0.5, Weight * ScoreNormalization.InsufficientDataWeightFactor,
                $"Muy pocas palabras ({sample.WordCount}) para medir repetición de trigramas.", HasData: false));
        }

        var words = sample.Words.Select(w => w.ToLowerInvariant()).ToList();
        var trigramCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var i = 0; i <= words.Count - N; i++)
        {
            var trigram = string.Join(' ', words[i], words[i + 1], words[i + 2]);
            trigramCounts[trigram] = trigramCounts.GetValueOrDefault(trigram) + 1;
        }

        var totalTrigrams = trigramCounts.Values.Sum();
        var repeatedTrigrams = trigramCounts.Values.Where(c => c > 1).Sum();
        var ratio = totalTrigrams == 0 ? 0.0 : repeatedTrigrams / (double)totalTrigrams;

        var score = ScoreNormalization.MapToProbability(ratio, valueAtZero: 0.05, valueAtOne: 0.35);

        return Task.FromResult(new SignalScore(
            Name, ratio, score, Weight,
            $"{repeatedTrigrams} de {totalTrigrams} trigramas repetidos (ratio={ratio:F2})."));
    }
}
