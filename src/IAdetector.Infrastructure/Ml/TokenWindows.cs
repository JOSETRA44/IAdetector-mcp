namespace IAdetector.Infrastructure.Ml;

/// <summary>
/// Ventanas de tokens para los modelos ONNX (Fase 4). Cada modelo tiene una ventana fija
/// (RoBERTa: 510 tokens de contenido; DistilGPT-2 aquí: 256) y antes de esta fase el texto
/// que no cabía se truncaba EN SILENCIO a la primera ventana: en un documento largo, la
/// señal solo «veía» el principio. Ahora, si el texto excede la ventana, se evalúan varias
/// ventanas repartidas uniformemente de principio a fin y se agrega el resultado, con lo
/// que el costo sigue acotado pero la cobertura es de todo el texto. Para textos que
/// caben en una ventana el comportamiento es idéntico al anterior.
///
/// El tope de ventanas es por señal porque el costo no es simétrico: una ventana de
/// perplexity (forward + barrido del vocabulario por posición) cuesta varias veces una
/// de RoBERTa, y la literatura de perplexity trabaja con 256–512 tokens en total; por eso
/// el clasificador usa hasta 4 ventanas y perplexity hasta 2 (medido en
/// docs/PERFORMANCE.md: con 4 y 4, un texto de 800 palabras costaba ~2,7× el truncado).
/// </summary>
public static class TokenWindows
{
    /// <summary>Tope de ventanas por defecto: acota el costo de inferencia en documentos muy largos.</summary>
    public const int MaxWindows = 4;

    /// <summary>
    /// Desplazamientos de inicio de cada ventana para un texto de <paramref name="totalTokens"/>
    /// tokens y ventanas de <paramref name="windowSize"/>. Un solo desplazamiento (0) si cabe.
    /// </summary>
    public static IReadOnlyList<int> Starts(int totalTokens, int windowSize, int maxWindows = MaxWindows)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxWindows);
        if (totalTokens <= windowSize)
        {
            return [0];
        }

        var wanted = Math.Min(maxWindows, (int)Math.Ceiling(totalTokens / (double)windowSize) + 1);
        var lastStart = totalTokens - windowSize;
        var starts = new List<int>(wanted);
        for (var i = 0; i < wanted; i++)
        {
            var start = wanted == 1 ? 0 : (int)Math.Round(i * (double)lastStart / (wanted - 1));
            if (starts.Count == 0 || starts[^1] != start)
            {
                starts.Add(start);
            }
        }

        return starts;
    }
}
