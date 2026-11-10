using System.Reflection;
using System.Text.Json;
using IAdetector.Core.Abstractions;
using IAdetector.Core.Domain;
using IAdetector.Infrastructure.Serialization;

namespace IAdetector.Infrastructure.Ensemble;

/// <summary>Forma del <c>HumanReferenceRanges.json</c> embebido (lo genera <c>scripts/calibration/make_reference_ranges.py</c>).</summary>
public sealed record HumanReferenceRangesFile(string Source, Dictionary<string, HumanRange> Ranges);

/// <summary>
/// Implementación de <see cref="IDetectionExplainer"/>: combina los coeficientes del ensemble
/// calibrado con los rangos humanos de referencia calculados sobre el corpus de calibración
/// (HC3; ver docs/CALIBRATION.md y <c>make_reference_ranges.py</c>). La guía por señal es
/// texto estático revisado a mano: describe cómo se mueve cada métrica, sin prometer que
/// seguirla garantice ningún resultado fuera de este detector.
/// </summary>
public sealed class DetectionExplainer : IDetectionExplainer
{
    private readonly EnsembleWeightsData _weights;
    private readonly IReadOnlyDictionary<string, HumanRange> _ranges;
    private readonly EnsembleWeightsData _spanishWeights;
    private readonly IReadOnlyDictionary<string, HumanRange> _spanishRanges;

    /// <summary>Guía de reescritura por señal cuando su puntuación supera el rango humano.</summary>
    private static readonly IReadOnlyDictionary<string, string> Guidance = new Dictionary<string, string>
    {
        ["burstiness"] = "Varía la longitud de las oraciones: alterna oraciones cortas con otras más largas "
            + "y con alguna subordinada; la uniformidad de longitud es justo lo que esta señal penaliza.",
        ["type-token-ratio"] = "Diversifica el vocabulario: sustituye las palabras y los conectores que más "
            + "se repiten por alternativas precisas, sin forzar sinónimos raros que no usaría una persona.",
        ["lexical-entropy"] = "El vocabulario está muy concentrado en unas pocas palabras: reparte el peso "
            + "introduciendo términos más variados y específicos del tema.",
        ["compression-ratio"] = "El texto es muy predecible en su estructura: varía cómo empiezan las "
            + "oraciones y los párrafos en vez de repetir la misma plantilla.",
        ["ngram-repetition"] = "Hay grupos de palabras repetidos literalmente: reformula esas expresiones "
            + "en lugar de reutilizarlas tal cual.",
        ["onnx-classifier"] = "El clasificador (un modelo entrenado, no una fórmula) ve el texto como propio "
            + "de una IA: ayudan el registro, el ritmo y sustituir las expresiones más genéricas por "
            + "formulaciones concretas y con voz propia.",
        ["perplexity"] = "El texto es muy predecible palabra a palabra: usa giros y combinaciones menos "
            + "habituales, sin introducir errores ni rarezas a propósito.",
    };

    private const string NoDataRecommendation =
        "Sin datos: esta señal no pudo medir el texto (demasiado corto para ella o modelo no descargado); no la uses para decidir.";

    private const string EnglishRangesResource = "IAdetector.Infrastructure.Ensemble.HumanReferenceRanges.json";
    private const string SpanishRangesResource = "IAdetector.Infrastructure.Ensemble.HumanReferenceRangesEs.json";

    public DetectionExplainer()
        : this(
            EnsembleWeightsResource.Load(), LoadRanges(EnglishRangesResource),
            EnsembleWeightsResource.LoadSpanish(), LoadRanges(SpanishRangesResource))
    {
    }

    /// <summary>Un solo perfil para ambos idiomas (tests).</summary>
    public DetectionExplainer(EnsembleWeightsData weights, IReadOnlyDictionary<string, HumanRange> ranges)
        : this(weights, ranges, weights, ranges)
    {
    }

    public DetectionExplainer(
        EnsembleWeightsData weights, IReadOnlyDictionary<string, HumanRange> ranges,
        EnsembleWeightsData spanishWeights, IReadOnlyDictionary<string, HumanRange> spanishRanges)
    {
        _weights = weights;
        _ranges = ranges;
        _spanishWeights = spanishWeights;
        _spanishRanges = spanishRanges;
    }

    public DetectionExplanation Explain(DetectionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        // Las contribuciones y los rangos deben ser los del perfil que produjo el
        // veredicto: el español desde la Fase 3.2, el de inglés en el resto.
        var weights = result.DetectedLanguage == TextLanguage.Spanish ? _spanishWeights : _weights;
        var ranges = result.DetectedLanguage == TextLanguage.Spanish ? _spanishRanges : _ranges;
        var diagnostics = result.Signals.Select(s => Diagnose(s, weights, ranges)).ToList();

        var actions = diagnostics
            .Where(d => d.HasData && d.Status == SignalStatus.AboveHumanRange)
            .OrderByDescending(d => d.LogitContribution ?? double.NegativeInfinity)
            .Take(5)
            .Select(d => $"{d.Name}: {d.Recommendation}")
            .ToList();

        return new DetectionExplanation(
            result.Verdict,
            result.AiProbability,
            result.Confidence,
            result.Signals.Count(s => s.HasData),
            result.Signals.Count,
            diagnostics,
            actions,
            result.DetectedLanguage,
            BuildWarnings(result.DetectedLanguage));
    }

    /// <summary>
    /// Avisos de fiabilidad por idioma. Desde la Fase 3.2 el español tiene su propio
    /// perfil calibrado (docs/SPANISH.md): el aviso ya no es «no es fiable», sino el
    /// nivel real alcanzado — accuracy agrupada 0,91 en su corpus, frente a 0,97 en
    /// inglés, con la IA de un número reducido de generadores.
    /// </summary>
    private static IReadOnlyList<string> BuildWarnings(TextLanguage language) => language switch
    {
        TextLanguage.Spanish =>
        [
            "Texto en español: se aplicó el perfil calibrado en español (Fase 3.2, "
            + "docs/SPANISH.md), con accuracy agrupada de 0,91 en su corpus de calibración "
            + "frente a 0,97 del inglés. Los modelos ONNX son de inglés y en este perfil "
            + "pesan ≈0: la decisión recae sobre todo en burstiness y las demás señales "
            + "estadísticas. El corpus de IA procede de pocos generadores, así que trata "
            + "el resultado con más cautela que uno en inglés.",
        ],
        TextLanguage.Unknown =>
        [
            "No se pudo identificar el idioma del texto; la calibración del ensemble es de "
            + "inglés, así que si el texto no está en inglés el resultado es menos fiable.",
        ],
        _ => [],
    };

    private static SignalDiagnostic Diagnose(
        SignalScore signal, EnsembleWeightsData weights, IReadOnlyDictionary<string, HumanRange> ranges)
    {
        var hasCoefficient = weights.Weights.TryGetValue(signal.Name, out var coefficient);
        double? contribution = hasCoefficient ? coefficient * signal.NormalizedScore : null;
        var hasRange = ranges.TryGetValue(signal.Name, out var range);

        if (!signal.HasData)
        {
            return new SignalDiagnostic(signal.Name, false, signal.RawValue, signal.NormalizedScore,
                hasCoefficient ? coefficient : null, contribution,
                hasRange ? range : null, SignalStatus.NoData, NoDataRecommendation);
        }

        if (!hasRange)
        {
            return new SignalDiagnostic(signal.Name, true, signal.RawValue, signal.NormalizedScore,
                hasCoefficient ? coefficient : null, contribution, null, SignalStatus.NoReference, string.Empty);
        }

        var (status, recommendation) = signal.NormalizedScore switch
        {
            var s when s > range!.P90 => (SignalStatus.AboveHumanRange,
                Guidance.TryGetValue(signal.Name, out var g) ? g : string.Empty),
            var s when s < range!.P10 => (SignalStatus.BelowHumanRange, string.Empty),
            _ => (SignalStatus.InHumanRange, string.Empty),
        };

        return new SignalDiagnostic(signal.Name, true, signal.RawValue, signal.NormalizedScore,
            hasCoefficient ? coefficient : null, contribution, range, status, recommendation);
    }

    private static IReadOnlyDictionary<string, HumanRange> LoadRanges(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Recurso embebido no encontrado: {resourceName}");
        var file = JsonSerializer.Deserialize(stream, IAdetectorJsonContext.Default.HumanReferenceRangesFile)
            ?? throw new InvalidOperationException("HumanReferenceRanges.json embebido vacío o inválido.");
        return file.Ranges;
    }
}
