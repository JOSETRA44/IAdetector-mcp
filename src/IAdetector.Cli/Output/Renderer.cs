using System.Globalization;
using System.Text;
using System.Text.Json;

namespace IAdetector.Cli.Output;

/// <summary>
/// Escribe el resultado de un comando en los cinco formatos del contrato.
/// </summary>
/// <remarks>
/// El JSON se construye con <see cref="Utf8JsonWriter"/> a mano en vez de serializar un objeto:
/// el sobre lleva formas heterogéneas (el manifiesto, el desglose por señal, filas de error de
/// un lote) y describirlas todas en el contexto de source-generation sería mucho ceremonial,
/// mientras que caer en serialización por reflexión rompería Native AOT en silencio.
/// Escribir los tokens es AOT-safe por construcción.
/// </remarks>
internal sealed class Renderer(OutputContext context)
{
    private readonly List<Row> _rows = [];
    private readonly StringBuilder _buffer = new();
    private bool _streamHeaderWritten;

    public int Count => _rows.Count;

    /// <summary>
    /// Agrega una fila. En <c>jsonl</c> y <c>csv</c> se emite de inmediato: un corpus de 500
    /// documentos debe ir apareciendo mientras corre, no después de varios minutos en silencio.
    /// </summary>
    public void Add(Row row)
    {
        _rows.Add(row);

        switch (context.Format)
        {
            case OutputFormat.Jsonl:
                Emit(WriteRowJson(row));
                break;
            case OutputFormat.Csv:
                if (!_streamHeaderWritten)
                {
                    // La cabecera se fija con la primera fila. En un lote las filas de error
                    // traen menos columnas, y `Row.Get` devuelve vacío para las que faltan.
                    Emit(string.Join(",", Row.HeaderOf(_rows).Select(CsvCell)));
                    _streamHeaderWritten = true;
                }

                Emit(string.Join(",", Header.Select(name => CsvCell(row.Get(name).Display))));
                break;
        }
    }

    private IReadOnlyList<string> Header => Row.HeaderOf(_rows);

    /// <summary>Cierra la salida: emite lo que el formato elegido todavía deba escribir.</summary>
    public void Finish(EnvelopeInfo info, string? human = null)
    {
        switch (context.Format)
        {
            case OutputFormat.Json:
                Emit(WriteEnvelope(info));
                break;
            case OutputFormat.Jsonl:
            case OutputFormat.Csv:
                break; // ya se emitió fila por fila
            case OutputFormat.Md:
                Emit(RenderMarkdown());
                break;
            default:
                Emit(human ?? RenderTable());
                break;
        }

        Flush();
    }

    /// <summary>Sobre de error: en formatos máquina va por stdout para que un pipeline lo parsee.</summary>
    public static int WriteError(OutputContext context, string command, CliError error)
    {
        if (context.Format is OutputFormat.Json or OutputFormat.Jsonl)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, Json.Options(context.Format == OutputFormat.Json)))
            {
                writer.WriteStartObject();
                writer.WriteBoolean("ok", false);
                writer.WriteString("command", command);
                writer.WriteString("source", EnvelopeInfo.SourceName);
                writer.WriteString("fetched_at", Timestamp());
                writer.WriteStartObject("error");
                writer.WriteString("code", error.Code);
                writer.WriteString("message", error.Message);
                writer.WriteString("hint", error.Hint);
                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            Console.Out.WriteLine(Encoding.UTF8.GetString(stream.ToArray()));
        }

        Console.Error.WriteLine($"Error [{error.Code}]: {error.Message}");
        Console.Error.WriteLine($"Sugerencia: {error.Hint}");
        return error.ExitCode;
    }

    // --- construcción JSON --------------------------------------------------

    private string WriteEnvelope(EnvelopeInfo info)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, Json.Options(indented: true)))
        {
            writer.WriteStartObject();
            writer.WriteBoolean("ok", true);
            writer.WriteString("command", info.Command);
            writer.WriteString("source", EnvelopeInfo.SourceName);
            writer.WriteString("fetched_at", Timestamp());
            writer.WriteBoolean("cached", info.Cached);
            writer.WriteNumber("count", _rows.Count);

            writer.WriteStartObject("meta");
            foreach (var (name, cell) in info.Meta)
            {
                writer.WritePropertyName(name);
                cell.WriteValue(writer);
            }

            writer.WriteEndObject();

            writer.WritePropertyName("data");
            if (info.WriteData is not null)
            {
                info.WriteData(writer);
            }
            else
            {
                writer.WriteStartObject();
                writer.WriteEndObject();
            }

            writer.WriteStartArray("rows");
            foreach (var row in _rows)
            {
                WriteRowObject(writer, row);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string WriteRowJson(Row row)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, Json.Options(indented: false)))
        {
            WriteRowObject(writer, row);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteRowObject(Utf8JsonWriter writer, Row row)
    {
        writer.WriteStartObject();
        foreach (var (name, cell) in row)
        {
            writer.WritePropertyName(name);
            cell.WriteValue(writer);
        }

        writer.WriteEndObject();
    }

    // --- formatos de texto --------------------------------------------------

    private string RenderTable()
    {
        if (_rows.Count == 0)
        {
            return "Sin resultados.";
        }

        var header = Header;
        var widths = header
            .Select(name => Math.Max(name.Length, _rows.Max(r => r.Get(name).Display.Length)))
            .ToArray();

        var lines = new List<string>
        {
            Line(header.Select((name, i) => name.PadRight(widths[i]))),
            Line(widths.Select(w => new string('-', w))),
        };

        lines.AddRange(_rows.Select(row =>
            Line(header.Select((name, i) => row.Get(name).Display.PadRight(widths[i])))));

        return string.Join(Environment.NewLine, lines);

        static string Line(IEnumerable<string> cells) => string.Join("  ", cells).TrimEnd();
    }

    private string RenderMarkdown()
    {
        if (_rows.Count == 0)
        {
            return "_Sin resultados._";
        }

        var header = Header;
        var lines = new List<string>
        {
            "| " + string.Join(" | ", header) + " |",
            "| " + string.Join(" | ", header.Select(_ => "---")) + " |",
        };

        lines.AddRange(_rows.Select(row =>
            "| " + string.Join(" | ", header.Select(name => MarkdownCell(row.Get(name).Display))) + " |"));

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>Una barra literal partiría la celda en dos columnas.</summary>
    private static string MarkdownCell(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);

    private static string CsvCell(string value) =>
        value.AsSpan().IndexOfAny(",\"\n\r") >= 0
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;

    // --- salida -------------------------------------------------------------

    private void Emit(string text)
    {
        if (context.OutputFile is not null)
        {
            _buffer.AppendLine(text);
            return;
        }

        Console.Out.WriteLine(text);
    }

    private void Flush()
    {
        if (context.OutputFile is null)
        {
            return;
        }

        File.WriteAllText(context.OutputFile.FullName, _buffer.ToString(), new UTF8Encoding(false));
        context.Note($"Escritas {_rows.Count} filas -> {context.OutputFile.FullName}");
    }

    internal static string Timestamp() =>
        DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
}

/// <summary>Lo que el sobre necesita más allá de las filas.</summary>
internal sealed record EnvelopeInfo(string Command)
{
    public const string SourceName = "iadetector";

    public bool Cached { get; init; }

    public IReadOnlyList<KeyValuePair<string, Cell>> Meta { get; init; } = [];

    /// <summary>Escribe el nodo <c>data</c>. Null emite un objeto vacío.</summary>
    public Action<Utf8JsonWriter>? WriteData { get; init; }
}
