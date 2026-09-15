using System.CommandLine;
using IAdetector.Core.Abstractions;

namespace IAdetector.Cli.Output;

internal enum OutputFormat
{
    Table,
    Json,
    Jsonl,
    Csv,
    Md,
}

/// <summary>
/// Los flags globales del contrato Arsenal (sección 1), compartidos por todos los comandos.
/// Se declaran una sola vez y se agregan a cada comando, en vez de en la raíz: System.CommandLine
/// no propaga las opciones de la raíz a los subcomandos, y un agente que escribe
/// <c>iadetector detect x.txt -f json</c> — la forma natural — no debería toparse con un error.
/// </summary>
internal static class GlobalOptions
{
    public static readonly string[] FormatNames = ["table", "json", "jsonl", "csv", "md"];

    public static Option<string> Format { get; } = new("--format", "-f")
    {
        Description = "Formato de salida: table | json | jsonl | csv | md.",
        DefaultValueFactory = _ => "table",
    };

    public static Option<FileInfo?> OutputFile { get; } = new("--output", "-o")
    {
        Description = "Escribe en FILE en vez de stdout.",
    };

    public static Option<bool> Quiet { get; } = new("--quiet", "-q")
    {
        Description = "Silencia notas y progreso en stderr.",
    };

    public static Option<bool> NoColor { get; } = new("--no-color")
    {
        Description = "Desactiva el color ANSI (también respeta la variable NO_COLOR).",
    };

    public static Option<bool> Refresh { get; } = new("--refresh")
    {
        Description = "Ignora el resultado cacheado, reanaliza y reescribe la caché.",
    };

    public static Option<bool> NoCache { get; } = new("--no-cache")
    {
        Description = "Ni lee ni escribe la caché de resultados.",
    };

    /// <summary>Alias histórico de <c>--format json</c>. Oculto: sigue funcionando, ya no se documenta.</summary>
    public static Option<bool> Json { get; } = new("--json")
    {
        Description = "Alias de --format json.",
        Hidden = true,
    };

    public static IReadOnlyList<Option> All { get; } =
        [Format, OutputFile, Quiet, NoColor, Refresh, NoCache, Json];

    /// <summary>Agrega los flags globales a <paramref name="command"/> y lo devuelve, para encadenar.</summary>
    public static Command WithGlobals(this Command command)
    {
        foreach (var option in All)
        {
            command.Options.Add(option);
        }

        return command;
    }

    public static OutputContext Resolve(ParseResult parseResult)
    {
        var raw = parseResult.GetValue(Format) ?? "table";
        if (parseResult.GetValue(Json))
        {
            raw = "json";
        }

        var format = raw.ToLowerInvariant() switch
        {
            "table" => OutputFormat.Table,
            "json" => OutputFormat.Json,
            "jsonl" => OutputFormat.Jsonl,
            "csv" => OutputFormat.Csv,
            "md" => OutputFormat.Md,
            _ => throw CliException.Usage(
                $"Formato desconocido: '{raw}'.",
                "Elige uno de: " + string.Join(", ", FormatNames) + "."),
        };

        var noColor = parseResult.GetValue(NoColor)
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));

        return new OutputContext(
            format,
            parseResult.GetValue(OutputFile),
            parseResult.GetValue(Quiet),
            noColor,
            CacheModeFrom(parseResult));
    }

    private static DetectionCacheMode CacheModeFrom(ParseResult parseResult)
    {
        // --no-cache gana sobre --refresh: pedir las dos cosas es contradictorio y la
        // interpretación segura es la que no toca el disco.
        if (parseResult.GetValue(NoCache))
        {
            return DetectionCacheMode.Disabled;
        }

        return parseResult.GetValue(Refresh) ? DetectionCacheMode.Refresh : DetectionCacheMode.ReadWrite;
    }
}

/// <summary>Los flags globales ya resueltos para una invocación.</summary>
internal sealed record OutputContext(
    OutputFormat Format,
    FileInfo? OutputFile,
    bool Quiet,
    bool NoColor,
    DetectionCacheMode CacheMode)
{
    /// <summary>Los formatos que un consumidor va a parsear: ahí stdout no admite decoración.</summary>
    public bool IsMachineFormat => Format is OutputFormat.Json or OutputFormat.Jsonl or OutputFormat.Csv;

    /// <summary>Nota para la persona. Siempre por stderr, nunca por stdout.</summary>
    public void Note(string message)
    {
        if (!Quiet)
        {
            Console.Error.WriteLine(message);
        }
    }
}
