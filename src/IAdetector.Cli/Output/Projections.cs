using System.Globalization;
using System.Text.Json;
using IAdetector.Core.Domain;
using IAdetector.Core.UseCases;

namespace IAdetector.Cli.Output;

/// <summary>
/// Convierte los resultados del dominio en filas planas y en el nodo <c>data</c> del sobre.
/// </summary>
/// <remarks>
/// La proyección plana es lo que hace analizable un corpus: con <c>batch -f csv</c> cada
/// documento sale como una fila con las mismas columnas y se abre tal cual en una hoja de
/// cálculo. Con <c>--verbose</c> cada señal agrega su propia columna, así que se puede ordenar
/// un corpus por, digamos, burstiness sin volver a correr nada.
/// </remarks>
internal static class Projections
{
    /// <summary>Columnas fijas de una detección, en el orden en que se leen.</summary>
    public static readonly string[] DetectionFields =
    [
        "path", "verdict", "ai_probability", "confidence", "total_words", "chunks", "sampled", "cached",
    ];

    public static Row DetectionRow(string path, DetectionOutcome outcome, bool verbose)
    {
        var row = BaseRow(path, outcome.Result, outcome.TotalWords, outcome.Cached);
        AddSignals(row, outcome.Result, verbose);
        return row;
    }

    public static Row BatchRow(BatchDetectionEntry entry, bool verbose)
    {
        if (entry.Result is null)
        {
            // Un archivo ilegible sale como su propia fila en vez de abortar el lote: con 500
            // documentos, que uno corrupto tire toda la corrida es inaceptable, y así el agente
            // puede reintentar exactamente los que fallaron.
            return new Row()
                .Add("path", entry.Path)
                .Add("ok", false)
                .Add("error_code", ErrorCodes.Api)
                .Add("error_message", entry.Error ?? "error desconocido");
        }

        var row = BaseRow(entry.Path, entry.Result, entry.TotalWords, entry.Cached).Add("ok", true);
        AddSignals(row, entry.Result, verbose);
        return row;
    }

    private static Row BaseRow(string path, DetectionResult result, int totalWords, bool cached)
    {
        var row = new Row()
            .Add("path", path)
            .Add("verdict", VerdictName(result.Verdict))
            .Add("ai_probability", result.AiProbability)
            .Add("confidence", result.Confidence)
            .Add("total_words", result.ChunkInfo?.TotalWords ?? totalWords)
            .Add("chunks", result.ChunkInfo?.TotalChunks ?? 1)
            .Add("sampled", result.ChunkInfo?.Sampled ?? false)
            .Add("cached", cached);

        return row;
    }

    private static void AddSignals(Row row, DetectionResult result, bool verbose)
    {
        if (!verbose)
        {
            return;
        }

        foreach (var signal in result.Signals)
        {
            // Prefijo `signal_` para que las columnas del desglose no puedan chocar con las fijas.
            row.Add("signal_" + signal.Name.Replace('-', '_'), signal.NormalizedScore);
        }
    }

    /// <summary>Nombre estable del veredicto: es lo que un agente va a comparar, no el texto humano.</summary>
    public static string VerdictName(Verdict verdict) => verdict switch
    {
        Verdict.Human => "human",
        Verdict.LikelyHuman => "likely_human",
        Verdict.Mixed => "mixed",
        Verdict.LikelyAI => "likely_ai",
        Verdict.AI => "ai",
        _ => "unknown",
    };

    /// <summary>Escribe el nodo <c>data</c> de una detección: el resultado completo, señales incluidas.</summary>
    public static void WriteDetection(Utf8JsonWriter writer, DetectionResult result, bool verbose)
    {
        writer.WriteStartObject();
        writer.WriteString("verdict", VerdictName(result.Verdict));
        writer.WriteNumber("ai_probability", Math.Round(result.AiProbability, 4));
        writer.WriteNumber("confidence", Math.Round(result.Confidence, 4));

        if (result.ChunkInfo is { } chunk)
        {
            writer.WriteStartObject("chunk_info");
            writer.WriteNumber("total_chunks", chunk.TotalChunks);
            writer.WriteNumber("total_words", chunk.TotalWords);
            writer.WriteBoolean("sampled", chunk.Sampled);
            writer.WriteEndObject();
        }
        else
        {
            writer.WriteNull("chunk_info");
        }

        if (verbose)
        {
            writer.WriteStartArray("signals");
            foreach (var signal in result.Signals)
            {
                writer.WriteStartObject();
                writer.WriteString("name", signal.Name);
                writer.WriteNumber("raw_value", Math.Round(signal.RawValue, 6));
                writer.WriteNumber("normalized_score", Math.Round(signal.NormalizedScore, 4));
                writer.WriteNumber("weight", Math.Round(signal.Weight, 4));
                writer.WriteString("explanation", signal.Explanation);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    /// <summary>Vista humana de una detección, la que ya existía antes del contrato Arsenal.</summary>
    public static string HumanDetail(string label, DetectionResult result, int totalWords, bool cached, bool verbose)
    {
        var lines = new List<string>
        {
            label,
            $"  Veredicto:       {VerdictName(result.Verdict)} ({result.AiProbability.ToString("P0", CultureInfo.InvariantCulture)} IA)",
            $"  Confianza:       {result.Confidence.ToString("P0", CultureInfo.InvariantCulture)}",
            $"  Palabras:        {(result.ChunkInfo?.TotalWords ?? totalWords).ToString(CultureInfo.InvariantCulture)}",
        };

        if (result.ChunkInfo is { } chunk)
        {
            var sampled = chunk.Sampled ? " — muestra representativa, no el documento completo" : string.Empty;
            lines.Add($"  Fragmentado:     {chunk.TotalChunks} fragmentos{sampled}");
        }

        if (cached)
        {
            lines.Add("  (resultado servido desde la caché — usa --refresh para reanalizar)");
        }

        if (verbose)
        {
            lines.Add("  Señales:");
            lines.AddRange(result.Signals.Select(s =>
                $"    - {s.Name,-20} score={s.NormalizedScore.ToString("F2", CultureInfo.InvariantCulture)}  " +
                $"peso={s.Weight.ToString("F2", CultureInfo.InvariantCulture)}  {s.Explanation}"));
        }

        return string.Join(Environment.NewLine, lines);
    }
}
