namespace IAdetector.Core.Domain;

/// <summary>
/// Un <see cref="DetectionResult"/> junto con el contexto que la capa de presentación necesita
/// y el resultado en sí no lleva: si salió de la caché en vez de recalcularse, y cuántas palabras
/// tenía el texto (<see cref="DetectionResult.ChunkInfo"/> solo existe cuando hubo fragmentación).
/// </summary>
public sealed record DetectionOutcome(DetectionResult Result, bool Cached, int TotalWords);
