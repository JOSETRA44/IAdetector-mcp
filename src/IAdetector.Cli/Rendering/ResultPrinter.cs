using System.Text.Json;
using IAdetector.Core.Domain;
using IAdetector.Core.UseCases;
using IAdetector.Infrastructure.Serialization;

namespace IAdetector.Cli.Rendering;

/// <summary>Salida de la CLI en texto legible (con color opcional) o JSON, compartiendo el mismo recorte de señales que el servidor MCP.</summary>
internal static class ResultPrinter
{
    public static void PrintJson(DetectionResult result, bool verbose) =>
        Console.WriteLine(JsonSerializer.Serialize(
            DetectionResultProjection.Apply(result, verbose), IAdetectorJsonContext.Default.DetectionResult));

    public static void PrintJson(IReadOnlyList<BatchDetectionEntry> entries, bool verbose) =>
        Console.WriteLine(JsonSerializer.Serialize(
            entries.Select(e => DetectionResultProjection.Apply(e, verbose)).ToList(),
            IAdetectorJsonContext.Default.ListBatchDetectionEntry));

    public static void PrintHuman(string label, DetectionResult result, bool verbose, bool noColor)
    {
        Console.WriteLine($"{label}");
        WriteVerdictLine(result, noColor);
        Console.WriteLine($"  Confianza:       {result.Confidence:P0}");

        if (result.ChunkInfo is { } chunk)
        {
            var sampledNote = chunk.Sampled ? " — muestra representativa, no se analizó el documento completo" : "";
            Console.WriteLine($"  Fragmentado:     {chunk.TotalChunks} fragmentos ({chunk.TotalWords} palabras totales){sampledNote}");
        }

        if (verbose)
        {
            Console.WriteLine("  Señales:");
            foreach (var signal in result.Signals)
            {
                Console.WriteLine($"    - {signal.Name,-20} score={signal.NormalizedScore:F2}  peso={signal.Weight:F2}  {signal.Explanation}");
            }
        }

        Console.WriteLine();
    }

    public static void PrintHuman(IReadOnlyList<BatchDetectionEntry> entries, bool verbose, bool noColor)
    {
        foreach (var entry in entries)
        {
            if (entry.Result is null)
            {
                WriteColored($"{entry.Path}: ERROR - {entry.Error}", ConsoleColor.Red, noColor);
                continue;
            }

            PrintHuman(entry.Path, entry.Result, verbose, noColor);
        }
    }

    private static void WriteVerdictLine(DetectionResult result, bool noColor)
    {
        var color = result.Verdict switch
        {
            Verdict.Human or Verdict.LikelyHuman => ConsoleColor.Green,
            Verdict.Mixed => ConsoleColor.Yellow,
            _ => ConsoleColor.Red,
        };

        WriteColored($"  Veredicto:       {result.Verdict} (probabilidad IA: {result.AiProbability:P0})", color, noColor);
    }

    private static void WriteColored(string line, ConsoleColor color, bool noColor)
    {
        if (noColor)
        {
            Console.WriteLine(line);
            return;
        }

        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(line);
        Console.ForegroundColor = previous;
    }
}
