using System.IO.Compression;
using System.Text;
using IAdetector.Core.Abstractions;
using IAdetector.Core.Domain;

namespace IAdetector.Infrastructure.Signals;

/// <summary>
/// Método de detección por compresión (estilo "Zippy"/AIDetx): comprime el texto con GZip y usa
/// el ratio comprimido/original como proxy de predictibilidad. Texto más predecible/repetitivo
/// (típico de IA) comprime mejor (ratio más bajo) que la prosa humana. GZip tiene overhead fijo
/// (cabecera + tablas Huffman) que distorsiona el ratio en textos cortos, de ahí el mínimo alto.
/// </summary>
public sealed class CompressionRatioAnalyzer : ISignalAnalyzer
{
    public string Name => "compression-ratio";

    private const int MinBytes = 2_000;
    private const double Weight = 1.2;

    public Task<SignalScore> AnalyzeAsync(TextSample sample, CancellationToken cancellationToken = default)
    {
        var originalBytes = Encoding.UTF8.GetByteCount(sample.Text);

        if (originalBytes < MinBytes)
        {
            return Task.FromResult(new SignalScore(
                Name, 0.0, 0.5, Weight * ScoreNormalization.InsufficientDataWeightFactor,
                $"Texto demasiado corto ({originalBytes} bytes) para un ratio de compresión confiable."));
        }

        var compressedBytes = Compress(sample.Text);
        var ratio = compressedBytes / (double)originalBytes;
        var score = ScoreNormalization.MapToProbability(ratio, valueAtZero: 0.42, valueAtOne: 0.28);

        return Task.FromResult(new SignalScore(
            Name, ratio, score, Weight,
            $"Ratio de compresión GZip={ratio:F2} ({originalBytes}→{compressedBytes} bytes)."));
    }

    private static int Compress(string text)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            gzip.Write(bytes, 0, bytes.Length);
        }

        return (int)output.Length;
    }
}
