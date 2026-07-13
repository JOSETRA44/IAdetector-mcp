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

    // Divide tras ./!/? seguido de espacio + mayúscula (o fin de cadena), para no cortar en
    // abreviaturas/decimales como "Sr." o "3.14" seguidos de minúscula.
    [GeneratedRegex(@"(?<=[.!?])\s+(?=[A-ZÁÉÍÓÚÑ¿¡\p{Lu}])")]
    private static partial Regex SentenceBoundaryRegex();

    [GeneratedRegex(@"[\p{L}\p{N}]+(?:['’-][\p{L}\p{N}]+)*")]
    private static partial Regex WordRegex();
}
