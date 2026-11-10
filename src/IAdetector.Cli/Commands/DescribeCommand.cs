using System.CommandLine;
using System.Text;
using System.Text.Json;
using IAdetector.Cli.Output;

namespace IAdetector.Cli.Commands;

/// <summary>
/// <c>describe</c> — manifiesto legible por máquina de toda la CLI (contrato Arsenal, sección 4).
/// </summary>
/// <remarks>
/// El manifiesto se recorre desde el árbol real de System.CommandLine, no de una tabla escrita
/// a mano: una lista paralela se desactualiza en el primer flag que alguien agregue, y entonces
/// un agente que confió en ella invoca algo que no existe.
/// </remarks>
internal static class DescribeCommand
{
    /// <summary>
    /// Lo que el árbol de comandos no sabe: qué columnas produce cada comando y cómo se ve una
    /// invocación real. Es lo que permite elegir el comando correcto sin leer el código.
    /// </summary>
    private static readonly Dictionary<string, CommandMeta> Meta = new(StringComparer.Ordinal)
    {
        ["detect"] = new(
            Projections.DetectionFields,
            SupportsAll: false,
            Network: false,
            [
                "iadetector detect informe.txt -f json",
                "iadetector detect --text \"un párrafo cualquiera\" --verbose -f json",
                "cat ensayo.md | iadetector detect -f json",
            ]),
        ["batch"] = new(
            [.. Projections.DetectionFields, "ok", "error_code", "error_message"],
            SupportsAll: true,
            Network: false,
            [
                "iadetector batch corpus/ -f csv -o corpus.csv",
                "iadetector batch corpus/ --verbose -f jsonl --max 500",
            ]),
        // Se busca primero por el nombre completo del comando y luego por el grupo: `models
        // list` sólo mira el disco, mientras que `models download` es lo único de todo el
        // proyecto que sale a la red, y decirlo mal haría que un consumidor esperase una caché
        // donde no la hay.
        ["models list"] = new(
            ["model", "state", "path", "display_name"],
            SupportsAll: false,
            Network: false,
            ["iadetector models list -f json"]),
        ["models download"] = new(
            ["model", "state", "path"],
            SupportsAll: false,
            Network: true,
            ["iadetector models download --model roberta-openai-detector -f json"]),
        ["models verify"] = new(
            ["model", "file", "state", "sha256"],
            SupportsAll: false,
            Network: false,
            ["iadetector models verify -f json"]),
        ["cache"] = new(
            ["path", "entries", "stale_entries", "size_bytes"],
            SupportsAll: false,
            Network: false,
            ["iadetector cache stats -f json", "iadetector cache clear"]),
        ["describe"] = new(
            ["command", "summary", "example"],
            SupportsAll: false,
            Network: false,
            ["iadetector describe --json", "iadetector describe detect --json"]),
    };

    private sealed record CommandMeta(
        IReadOnlyList<string> RowFields,
        bool SupportsAll,
        bool Network,
        IReadOnlyList<string> Examples);

    public static Command Create(RootCommand root, string version)
    {
        var target = new Argument<string?>("comando")
        {
            Description = "Describe sólo este comando (por defecto, toda la CLI).",
            Arity = ArgumentArity.ZeroOrOne,
        };

        var command = new Command("describe", "Manifiesto legible por máquina de la CLI, para agentes y scripts.")
        {
            target,
        };

        command.WithGlobals();

        command.SetAction(parseResult => Guard.Run(parseResult, "describe", context =>
        {
            var name = parseResult.GetValue(target);
            var json = BuildManifest(root, version, name);

            if (name is not null && json is null)
            {
                throw CliException.Usage(
                    $"Comando desconocido: '{name}'.",
                    "Ejecuta `iadetector describe --json` para ver todos.");
            }

            if (context.Format == OutputFormat.Table)
            {
                RenderHuman(root, version);
                return ExitCodes.Ok;
            }

            Console.Out.WriteLine(json);
            return ExitCodes.Ok;
        }));

        return command;
    }

    /// <summary>Devuelve el manifiesto como JSON, o null si se pidió un comando que no existe.</summary>
    internal static string? BuildManifest(RootCommand root, string version, string? only = null)
    {
        var commands = root.Subcommands.Where(c => !c.Hidden).ToList();
        if (only is not null)
        {
            commands = commands.Where(c => string.Equals(c.Name, only, StringComparison.Ordinal)).ToList();
            if (commands.Count == 0)
            {
                return null;
            }
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, Json.Options(indented: true)))
        {
            writer.WriteStartObject();
            writer.WriteString("name", "iadetector");
            writer.WriteString("version", version);

            writer.WriteStartObject("source");
            writer.WriteString("api", "local");
            writer.WriteNull("base_url");
            writer.WriteString("auth", "none");
            writer.WriteString(
                "description",
                "Detección de texto generado por IA 100% local: siete señales estadísticas y " +
                "dos modelos ONNX, sin ninguna API externa.");
            writer.WriteEndObject();

            writer.WriteStartArray("formats");
            foreach (var format in GlobalOptions.FormatNames)
            {
                writer.WriteStringValue(format);
            }

            writer.WriteEndArray();

            writer.WriteStartObject("exit_codes");
            foreach (var (code, _, meaning) in ExitCodes.Table)
            {
                writer.WriteString(code.ToString(System.Globalization.CultureInfo.InvariantCulture), meaning);
            }

            writer.WriteEndObject();

            writer.WriteStartArray("global_options");
            foreach (var option in GlobalOptions.All.Where(o => !o.Hidden))
            {
                WriteOption(writer, option);
            }

            writer.WriteEndArray();

            writer.WriteStartArray("commands");
            foreach (var child in commands)
            {
                WriteCommand(writer, child, [child.Name]);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteCommand(Utf8JsonWriter writer, Command command, List<string> path)
    {
        var children = command.Subcommands.Where(c => !c.Hidden).ToList();
        if (children.Count > 0)
        {
            foreach (var child in children)
            {
                WriteCommand(writer, child, [.. path, child.Name]);
            }

            return;
        }

        var name = string.Join(" ", path);
        var meta = Meta.GetValueOrDefault(name) ?? Meta.GetValueOrDefault(path[0]);

        writer.WriteStartObject();
        writer.WriteString("name", name);
        writer.WriteString("summary", command.Description ?? name);

        writer.WriteStartArray("arguments");
        foreach (var argument in command.Arguments)
        {
            writer.WriteStartObject();
            writer.WriteString("name", argument.Name);
            writer.WriteString("type", argument.ValueType.Name);
            writer.WriteBoolean("required", argument.Arity.MinimumNumberOfValues > 0);
            writer.WriteString("help", argument.Description ?? string.Empty);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WriteStartArray("options");
        foreach (var option in command.Options.Where(o => !o.Hidden && !IsGlobal(o)))
        {
            WriteOption(writer, option);
        }

        writer.WriteEndArray();

        writer.WriteStartArray("row_fields");
        foreach (var field in meta?.RowFields ?? [])
        {
            writer.WriteStringValue(field);
        }

        writer.WriteEndArray();

        writer.WriteBoolean("supports_all", meta?.SupportsAll ?? false);

        // Le dice a un consumidor si el resultado puede venir de la caché o si el comando
        // se responde sin salir de la máquina.
        writer.WriteBoolean("network", meta?.Network ?? false);

        writer.WriteStartArray("examples");
        foreach (var example in meta?.Examples ?? [])
        {
            writer.WriteStringValue(example);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static bool IsGlobal(Option option) => GlobalOptions.All.Any(g => ReferenceEquals(g, option));

    private static void WriteOption(Utf8JsonWriter writer, Option option)
    {
        writer.WriteStartObject();
        writer.WriteString("name", option.Name);
        writer.WriteStartArray("aliases");
        foreach (var alias in option.Aliases)
        {
            writer.WriteStringValue(alias);
        }

        writer.WriteEndArray();
        writer.WriteString("type", option.ValueType.Name);
        writer.WriteBoolean("required", option.Required);
        writer.WriteString("help", option.Description ?? string.Empty);
        writer.WriteEndObject();
    }

    private static void RenderHuman(RootCommand root, string version)
    {
        Console.Out.WriteLine($"iadetector {version} — detección local de texto generado por IA");
        Console.Out.WriteLine();
        Console.Out.WriteLine($"Formatos: {string.Join(", ", GlobalOptions.FormatNames)}");
        Console.Out.WriteLine();
        Console.Out.WriteLine("Comandos:");
        foreach (var command in root.Subcommands.Where(c => !c.Hidden))
        {
            Console.Out.WriteLine($"  {command.Name,-12} {command.Description}");
            var example = Meta.GetValueOrDefault(command.Name)?.Examples.FirstOrDefault();
            if (example is not null)
            {
                Console.Out.WriteLine($"  {string.Empty,-12} ej. {example}");
            }
        }

        Console.Out.WriteLine();
        Console.Out.WriteLine("Manifiesto completo: iadetector describe --json");
    }
}
