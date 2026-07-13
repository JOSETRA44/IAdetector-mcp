namespace IAdetector.Core.Domain;

/// <summary>
/// Resultado agregado de analizar un texto: veredicto final, probabilidad de IA,
/// confianza (qué tan de acuerdo están las señales entre sí) y el desglose por señal.
/// </summary>
public sealed record DetectionResult(
    Verdict Verdict,
    double AiProbability,
    double Confidence,
    IReadOnlyList<SignalScore> Signals,
    ChunkInfo? ChunkInfo = null);
