namespace IAdetector.Core.Domain;

/// <summary>
/// Veredicto final de una detección, derivado de <see cref="DetectionResult.AiProbability"/>.
/// </summary>
public enum Verdict
{
    Human,
    LikelyHuman,
    Mixed,
    LikelyAI,
    AI,
}
