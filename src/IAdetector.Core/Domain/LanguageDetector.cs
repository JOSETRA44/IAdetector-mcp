namespace IAdetector.Core.Domain;

/// <summary>Idiomas que el detector sabe distinguir hoy. La calibración del ensemble es de inglés.</summary>
public enum TextLanguage
{
    Unknown,
    English,
    Spanish,
}

/// <summary>
/// Detección de idioma ligera y 100% local, por frecuencia de palabras funcionales
/// («el/la/de/en…» frente a «the/of/and…»). No pretende competir con un clasificador
/// de idioma completo: decide solo cuando hay evidencia clara (mínimo de palabras
/// funcionales y mayoría suficiente) y devuelve <see cref="TextLanguage.Unknown"/>
/// en cualquier otro caso, porque su uso es decidir qué avisos de fiabilidad mostrar,
/// no clasificar por clasificar.
/// </summary>
public static class LanguageDetector
{
    private static readonly HashSet<string> SpanishFunctionWords = new(StringComparer.Ordinal)
    {
        "el", "la", "los", "las", "un", "una", "unos", "unas", "de", "del", "en", "y", "o",
        "que", "es", "son", "con", "por", "para", "al", "lo", "como", "más", "pero", "sus",
        "se", "no", "sí", "también", "cuando", "muy", "sin", "sobre", "entre", "hasta",
        "desde", "donde", "este", "esta", "estos", "estas", "ese", "esa", "mi", "su", "ya",
        "fue", "han", "ser", "está", "están", "hay", "le", "les", "ni", "si",
    };

    private static readonly HashSet<string> EnglishFunctionWords = new(StringComparer.Ordinal)
    {
        "the", "of", "and", "a", "an", "in", "is", "you", "that", "it", "he", "was", "for",
        "on", "are", "with", "as", "i", "his", "they", "be", "at", "one", "have", "this",
        "from", "or", "had", "by", "were", "but", "not", "what", "all", "we", "can", "her",
        "has", "there", "been", "if", "will", "would", "do", "does", "did", "are", "their",
        "which", "who", "when", "where", "how", "an", "my", "me", "him", "she", "we", "us",
    };

    /// <summary>Mínimo de palabras funcionales reconocidas para decidir algo.</summary>
    private const int MinFunctionWordHits = 5;

    /// <summary>Fracción mínima de los aciertos que debe reunir el idioma ganador.</summary>
    private const double MinWinnerShare = 0.65;

    public static TextLanguage Detect(TextSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        var spanish = 0;
        var english = 0;
        foreach (var word in sample.Words)
        {
            var w = word.ToLowerInvariant();
            if (SpanishFunctionWords.Contains(w))
            {
                spanish++;
            }
            else if (EnglishFunctionWords.Contains(w))
            {
                english++;
            }
        }

        var total = spanish + english;
        if (total < MinFunctionWordHits)
        {
            return TextLanguage.Unknown;
        }

        var winnerShare = (double)Math.Max(spanish, english) / total;
        if (winnerShare < MinWinnerShare)
        {
            return TextLanguage.Unknown;
        }

        return spanish > english ? TextLanguage.Spanish : TextLanguage.English;
    }
}
