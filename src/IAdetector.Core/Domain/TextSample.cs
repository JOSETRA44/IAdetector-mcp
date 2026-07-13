namespace IAdetector.Core.Domain;

/// <summary>
/// Texto normalizado listo para ser analizado, junto con métricas básicas pre-calculadas
/// que varias señales necesitan (evita recalcular la tokenización en cada analizador).
/// </summary>
public sealed class TextSample
{
    public required string Text { get; init; }
    public required IReadOnlyList<string> Sentences { get; init; }
    public required IReadOnlyList<string> Words { get; init; }

    public int CharacterCount => Text.Length;
    public int WordCount => Words.Count;
    public int SentenceCount => Sentences.Count;

    public static TextSample Create(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var normalized = text.Trim();
        return new TextSample
        {
            Text = normalized,
            Sentences = TextTokenizer.SplitSentences(normalized),
            Words = TextTokenizer.SplitWords(normalized),
        };
    }
}
