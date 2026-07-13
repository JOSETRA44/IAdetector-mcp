namespace IAdetector.Core.Domain;

/// <summary>
/// Presente cuando un texto largo fue dividido en fragmentos para su análisis;
/// describe cuántos fragmentos hubo en total (el resultado agregado ya combina todos).
/// </summary>
/// <param name="TotalChunks">Número total de fragmentos analizados.</param>
/// <param name="TotalWords">Palabras totales del texto original, antes de fragmentar.</param>
public sealed record ChunkInfo(int TotalChunks, int TotalWords);
