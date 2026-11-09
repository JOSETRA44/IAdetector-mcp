namespace IAdetector.Core.Domain;

/// <summary>Resultado global de comparar una reescritura con su original.</summary>
public static class ComparisonOutcome
{
    /// <summary>La probabilidad de IA bajó y las salvaguardas no se dispararon.</summary>
    public const string Improved = "mejora";

    /// <summary>La probabilidad de IA subió.</summary>
    public const string Worsened = "empeora";

    /// <summary>La probabilidad no se movió (mismo texto o cambio despreciable).</summary>
    public const string Unchanged = "sin_cambio";

    /// <summary>
    /// No se puede concluir: falta evidencia en alguna versión o la similitud semántica
    /// cayó por debajo del umbral (la reescritura puede haber perdido significado).
    /// </summary>
    public const string Inconclusive = "no_concluyente";
}

/// <summary>
/// Cambio de una señal entre el original y la reescritura. <paramref name="LogitDelta"/> es
/// la diferencia de contribuciones al logit (β × Δpuntuación): cuánto del cambio total de
/// probabilidad explica esta señal. Null si alguna versión no tiene datos o no hay coeficiente.
/// </summary>
public sealed record SignalDelta(
    string Name,
    bool HasDataBefore,
    bool HasDataAfter,
    double ScoreBefore,
    double ScoreAfter,
    double Delta,
    double? LogitDelta);

/// <summary>
/// Comparación sin estado entre un texto y su reescritura: la pieza del bucle de un agente
/// que itera reescribiendo. Además del cambio de probabilidad y del desglose por señal,
/// incluye salvaguardas contra la «optimización vacía»: si la reescritura se aleja demasiado
/// del vocabulario de contenido del original, el resultado es <c>no_concluyente</c> aunque
/// la probabilidad baje, porque el texto puede haber perdido su significado.
/// </summary>
/// <param name="SemanticSimilarity">Jaccard sobre las palabras de contenido (≥4 letras) de ambas versiones, 0..1. Heurística léxica, no una medida semántica real.</param>
public sealed record VersionComparison(
    DetectionResult Before,
    DetectionResult After,
    double DeltaProbability,
    string Outcome,
    double SemanticSimilarity,
    IReadOnlyList<SignalDelta> SignalDeltas,
    IReadOnlyList<string> Warnings);
