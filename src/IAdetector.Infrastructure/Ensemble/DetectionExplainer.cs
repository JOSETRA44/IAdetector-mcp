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

    public DetectionExplainer()
        : this(EnsembleWeightsResource.Load(), LoadRanges())
    {
    }

    public DetectionExplainer(EnsembleWeightsData weights, IReadOnlyDictionary<string, HumanRange> ranges)
    {
        _weights = weights;
        _ranges = ranges;
    }

    public DetectionExplanation Explain(DetectionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var diagnostics = result.Signals.Select(Diagnose).ToList();

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
    /// Avisos de fiabilidad por idioma, basados en la medición de la Fase 3
    /// (docs/SPANISH.md): en español el problema medido no son los falsos positivos,
    /// sino la ceguera — los dos modelos ONNX son de inglés y puntúan ~0 tanto el
    /// texto humano como el de IA en español, así que un veredicto «humano» en
    /// español no distingue nada hoy.
    /// </summary>
    private static IReadOnlyList<string> BuildWarnings(TextLanguage language) => language switch
    {
        TextLanguage.Spanish =>
        [
            "Texto en español: la calibración del ensemble es de inglés (HC3) y los dos modelos "
            + "ONNX también. Medido en la Fase 3 (docs/SPANISH.md): en español ambos modelos "
            + "puntúan ≈0 tanto para texto humano como para texto de IA, así que el detector "
            + "apenas detecta IA en español y un veredicto «humano» aquí NO es fiable. Las "
            + "señales estadísticas sí miden, pero su peso calibrado no basta para compensarlo.",
        ],
        TextLanguage.Unknown =>
        [
            "No se pudo identificar el idioma del texto; la calibración del ensemble es de "
            + "inglés, así que si el texto no está en inglés el resultado es menos fiable.",
        ],
        _ => [],
    };

    private SignalDiagnostic Diagnose(SignalScore signal)
    {
        var hasCoefficient = _weights.Weights.TryGetValue(signal.Name, out var coefficient);
        double? contribution = hasCoefficient ? coefficient * signal.NormalizedScore : null;
        var hasRange = _ranges.TryGetValue(signal.Name, out var range);

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

    private static IReadOnlyDictionary<string, HumanRange> LoadRanges()
    {
        const string resourceName = "IAdetector.Infrastructure.Ensemble.HumanReferenceRanges.json";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Recurso embebido no encontrado: {resourceName}");
        var file = JsonSerializer.Deserialize(stream, IAdetectorJsonContext.Default.HumanReferenceRangesFile)
            ?? throw new InvalidOperationException("HumanReferenceRanges.json embebido vacío o inválido.");
        return file.Ranges;
    }
}
