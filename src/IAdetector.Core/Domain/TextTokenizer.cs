using System.Text.RegularExpressions;

namespace IAdetector.Core.Domain;

/// <summary>
/// Segmentación ligera de texto (oraciones/palabras) compartida por las señales estadísticas.
/// Deliberadamente simple (basada en reglas, sin modelo de lenguaje): suficiente para métricas
/// agregadas como burstiness o TTR, donde el detalle exacto de un caso límite no cambia el resultado.
/// </summary>
public static partial class TextTokenizer
{
    public static IReadOnlyList<string> SplitSentences(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return SentenceBoundaryRegex()
            .Split(text)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
    }

    public static IReadOnlyList<string> SplitWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return WordRegex()
            .Matches(text)
            .Select(m => m.Value)
            .ToList();
    }

    /// <summary>
    /// Igual que <see cref="SplitWords"/> pero devuelve la posición de cada palabra en <paramref name="text"/>
    /// en vez del valor ya extraído. Permite recortar sub-cadenas del texto original que preservan
    /// puntuación/mayúsculas (a diferencia de reconstruir texto uniendo palabras con espacios, que
    /// destruiría los límites de oración que <see cref="SplitSentences"/> necesita detectar).
    /// </summary>
    public static IReadOnlyList<(int Start, int Length)> SplitWordSpans(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return WordRegex()
            .Matches(text)
            .Select(m => (m.Index, m.Length))
            .ToList();
    }

    // Divide tras ./!/? seguido de espacio + mayúscula (o fin de cadena), para no cortar en
    // abreviaturas/decimales como "Sr." o "3.14" seguidos de minúscula.
    [GeneratedRegex(@"(?<=[.!?])\s+(?=[A-ZÁÉÍÓÚÑ¿¡\p{Lu}])")]
    private static partial Regex SentenceBoundaryRegex();

    [GeneratedRegex(@"[\p{L}\p{N}]+(?:['’-][\p{L}\p{N}]+)*")]
    private static partial Regex WordRegex();
}
