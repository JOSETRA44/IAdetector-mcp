namespace IAdetector.Core.Domain;

/// <summary>
/// Presente cuando un texto largo fue dividido en fragmentos para su análisis;
/// describe cuántos fragmentos hubo en total (el resultado agregado ya combina todos).
/// </summary>
/// <param name="TotalChunks">Número de fragmentos efectivamente analizados (puede ser menor al total natural si <paramref name="Sampled"/> es true).</param>
/// <param name="TotalWords">Palabras totales del texto original, antes de fragmentar.</param>
/// <param name="Sampled">True si el documento era tan largo que se analizó una muestra representativa de fragmentos en vez de todos.</param>
public sealed record ChunkInfo(int TotalChunks, int TotalWords, bool Sampled = false);
