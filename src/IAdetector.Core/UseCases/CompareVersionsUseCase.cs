using IAdetector.Core.Abstractions;
using IAdetector.Core.Domain;

namespace IAdetector.Core.UseCases;

/// <summary>
/// Compara un texto con su reescritura sin guardar estado entre llamadas: detecta ambas
/// versiones, calcula el cambio de probabilidad y por señal (con su contribución al logit
/// vía <see cref="IDetectionExplainer"/>) y aplica las salvaguardas de significado.
///
/// Salvaguardas (anti-Goodhart): una reescritura puede bajar la probabilidad vaciando el
/// texto de contenido. La similitud se estima con Jaccard sobre palabras de contenido
/// (≥4 letras); por debajo de <see cref="MinSemanticSimilarity"/> el resultado es
/// <c>no_concluyente</c> por mucho que baje la probabilidad. Es una heurística léxica
/// barata y local — no sustituye a una revisión humana del significado —, pensada para
/// frenar el bucle, no para certificar paráfrasis.
/// </summary>
public sealed class CompareVersionsUseCase(DetectTextUseCase detectText, IDetectionExplainer explainer)
{
    /// <summary>Similitud léxica mínima para dar por válida una mejora.</summary>
    public const double MinSemanticSimilarity = 0.5;

    /// <summary>Longitud de palabra mínima para contarla como palabra de contenido.</summary>
    private const int MinContentWordLength = 4;

    public async Task<VersionComparison> CompareAsync(
        string originalText, string rewrittenText, CancellationToken cancellationToken = default)
    {
        var before = await detectText.DetectAsync(originalText, cancellationToken).ConfigureAwait(false);
        var after = await detectText.DetectAsync(rewrittenText, cancellationToken).ConfigureAwait(false);

        var beforeExplanation = explainer.Explain(before);
        var afterExplanation = explainer.Explain(after);

        var deltas = BuildDeltas(beforeExplanation, afterExplanation);
        var similarity = ContentWordJaccard(originalText, rewrittenText);
        var deltaProbability = after.AiProbability - before.AiProbability;

        var warnings = new List<string>();
        var insufficientEvidence =
            beforeExplanation.SignalsWithData == 0 || afterExplanation.SignalsWithData == 0;
        if (insufficientEvidence)
        {
            warnings.Add("Evidencia insuficiente en alguna de las dos versiones (ninguna señal pudo "
                + "medirla): la comparación de probabilidades no es fiable.");
        }

        if (similarity < MinSemanticSimilarity)
        {
            warnings.Add($"La reescritura conserva solo el {similarity:P0} del vocabulario de contenido "
                + $"del original (mínimo {MinSemanticSimilarity:P0}): puede haber perdido significado, "
                + "así que una bajada de probabilidad no cuenta como mejora.");
        }

        var lengthRatio = WordRatio(originalText, rewrittenText);
        if (lengthRatio is < 0.5 or > 2.0)
        {
            warnings.Add($"La longitud cambió mucho (la reescritura tiene el {lengthRatio:P0} de las "
                + "palabras del original): revisa que no se haya perdido ni añadido contenido.");
        }

        var outcome = insufficientEvidence || similarity < MinSemanticSimilarity
            ? ComparisonOutcome.Inconclusive
            : deltaProbability switch
            {
                < -0.001 => ComparisonOutcome.Improved,
                > 0.001 => ComparisonOutcome.Worsened,
                _ => ComparisonOutcome.Unchanged,
            };

        return new VersionComparison(before, after, deltaProbability, outcome, similarity, deltas, warnings);
    }

    private static IReadOnlyList<SignalDelta> BuildDeltas(
        DetectionExplanation before, DetectionExplanation after)
    {
        var afterByName = after.Signals.ToDictionary(s => s.Name);
        var deltas = new List<SignalDelta>();
        foreach (var b in before.Signals)
        {
            if (!afterByName.TryGetValue(b.Name, out var a))
            {
                continue;
            }

            double? logitDelta = b.LogitContribution is not null && a.LogitContribution is not null
                ? a.LogitContribution.Value - b.LogitContribution.Value
                : null;

            deltas.Add(new SignalDelta(
                b.Name, b.HasData, a.HasData,
                b.NormalizedScore, a.NormalizedScore,
                a.NormalizedScore - b.NormalizedScore, logitDelta));
        }

        return deltas;
    }

    /// <summary>Jaccard entre los conjuntos de palabras de contenido (≥4 letras, minúsculas) de dos textos.</summary>
    public static double ContentWordJaccard(string first, string second)
    {
        var a = ContentWords(first);
        var b = ContentWords(second);
        if (a.Count == 0 && b.Count == 0)
        {
            return 1.0;
        }

        if (a.Count == 0 || b.Count == 0)
        {
            return 0.0;
        }

        var intersection = a.Intersect(b).Count();
        var union = a.Count + b.Count - intersection;
        return (double)intersection / union;
    }

    private static HashSet<string> ContentWords(string text) =>
        TextSample.Create(text).Words
            .Select(w => w.ToLowerInvariant())
            .Where(w => w.Length >= MinContentWordLength)
            .ToHashSet();

    private static double WordRatio(string original, string rewritten)
    {
        var originalWords = TextSample.Create(original).WordCount;
        return originalWords == 0 ? 1.0 : (double)TextSample.Create(rewritten).WordCount / originalWords;
    }
}
