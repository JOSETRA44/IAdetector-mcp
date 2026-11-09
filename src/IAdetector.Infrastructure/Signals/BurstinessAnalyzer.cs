using IAdetector.Core.Abstractions;
using IAdetector.Core.Domain;

namespace IAdetector.Infrastructure.Signals;

/// <summary>
/// Mide la "burstiness" (variabilidad) de la longitud de las oraciones, en palabras, usando el
/// parámetro de Goh-Barabási B = (σ-μ)/(σ+μ). Para longitudes de oración (en palabras), σ rara
/// vez supera a μ, así que B es casi siempre negativo en la práctica: cerca de 0 cuando las
/// oraciones varían mucho entre sí (bursty, típico de texto humano) y fuertemente negativo cuando
/// son muy uniformes (típico de texto de IA). Los umbrales son heurísticos iniciales; se
/// recalibrarán en la Fase 3 con datos reales (HC3).
/// </summary>
public sealed class BurstinessAnalyzer : ISignalAnalyzer
{
    public string Name => "burstiness";

    private const int MinSentences = 5;
    private const double Weight = 1.0;

    public Task<SignalScore> AnalyzeAsync(TextSample sample, CancellationToken cancellationToken = default)
    {
        if (sample.SentenceCount < MinSentences)
        {
            return Task.FromResult(new SignalScore(
                Name, 0.0, 0.5, Weight * ScoreNormalization.InsufficientDataWeightFactor,
                $"Muy pocas oraciones ({sample.SentenceCount}) para medir burstiness de forma confiable.", HasData: false));
        }

        var lengths = sample.Sentences
            .Select(s => (double)TextTokenizer.SplitWords(s).Count)
            .Where(l => l > 0)
            .ToList();

        var mean = lengths.Average();
        var variance = lengths.Sum(l => (l - mean) * (l - mean)) / lengths.Count;
        var stdev = Math.Sqrt(variance);

        if (stdev + mean == 0)
        {
            return Task.FromResult(new SignalScore(
                Name, 0.0, 0.5, Weight * ScoreNormalization.InsufficientDataWeightFactor,
                "Longitud de oración degenerada (todas vacías).", HasData: false));
        }

        var burstiness = (stdev - mean) / (stdev + mean);
        var score = ScoreNormalization.MapToProbability(burstiness, valueAtZero: -0.05, valueAtOne: -0.65);

        return Task.FromResult(new SignalScore(
            Name, burstiness, score, Weight,
            $"Burstiness B={burstiness:F2} (media {mean:F1} palabras/oración, σ={stdev:F1}) sobre {sample.SentenceCount} oraciones."));
    }
}
