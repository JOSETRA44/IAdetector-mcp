using System.CommandLine;
using IAdetector.Cli.Rendering;
using IAdetector.Core.UseCases;

namespace IAdetector.Cli.Commands;

internal static class BatchCommand
{
    public static Command Create(BatchDetectUseCase batchDetect)
    {
        var directoryArgument = new Argument<DirectoryInfo>("directory") { Description = "Directorio a analizar." };
        var patternOption = new Option<string>("--pattern")
        {
            Description = "Patrón glob de archivos a incluir.",
            DefaultValueFactory = _ => "*.txt",
        };
        var recursiveOption = new Option<bool>("--recursive") { Description = "Buscar también en subdirectorios." };
        var jsonOption = new Option<bool>("--json") { Description = "Salida en formato JSON." };
        var verboseOption = new Option<bool>("--verbose") { Description = "Incluye el desglose de señales por archivo." };
        var noColorOption = new Option<bool>("--no-color") { Description = "Desactiva colores en la salida." };
        var outputOption = new Option<FileInfo?>("--output")
        {
            Description = "Guarda los resultados en formato CSV en este archivo, además de imprimirlos.",
        };
        var maxFilesOption = new Option<int>("--max-files")
        {
            Description = "Número máximo de archivos a procesar (evita quedarse horas en un directorio enorme por error).",
            DefaultValueFactory = _ => 1000,
        };

        var command = new Command("batch", "Analiza todos los archivos de texto de un directorio.")
        {
            directoryArgument,
            patternOption,
            recursiveOption,
            jsonOption,
            verboseOption,
            noColorOption,
            outputOption,
            maxFilesOption,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var directory = parseResult.GetValue(directoryArgument);
            var pattern = parseResult.GetValue(patternOption) ?? "*.txt";
            var recursive = parseResult.GetValue(recursiveOption);
            var json = parseResult.GetValue(jsonOption);
            var verbose = parseResult.GetValue(verboseOption);
            var noColor = parseResult.GetValue(noColorOption);
            var output = parseResult.GetValue(outputOption);
            var maxFiles = parseResult.GetValue(maxFilesOption);

            if (directory is null || !directory.Exists)
            {
                Console.Error.WriteLine($"Error: no se encontró el directorio '{directory?.FullName}'.");
                return 1;
            }

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.EnumerateFiles(directory.FullName, pattern, searchOption).Take(maxFiles).ToList();

            if (files.Count == 0)
            {
                Console.Error.WriteLine("No se encontraron archivos que coincidan con el patrón.");
                return 0;
            }

            var entries = await batchDetect.DetectFilesAsync(files, maxDegreeOfParallelism: 4, cancellationToken)
                .ConfigureAwait(false);

            if (json)
            {
                ResultPrinter.PrintJson(entries, verbose);
            }
            else
            {
                ResultPrinter.PrintHuman(entries, verbose, noColor);
            }

            if (output is not null)
            {
                await WriteCsvAsync(output.FullName, entries).ConfigureAwait(false);
            }

            return 0;
        });

        return command;
    }

    private static async Task WriteCsvAsync(string path, IReadOnlyList<BatchDetectionEntry> entries)
    {
        await using var writer = new StreamWriter(path);
        await writer.WriteLineAsync("path,verdict,ai_probability,confidence,error");

        foreach (var entry in entries)
        {
            var line = entry.Result is not null
                ? $"{Csv(entry.Path)},{entry.Result.Verdict},{entry.Result.AiProbability:F4},{entry.Result.Confidence:F4},"
                : $"{Csv(entry.Path)},,,,{Csv(entry.Error ?? string.Empty)}";
            await writer.WriteLineAsync(line);
        }
    }

    private static string Csv(string value) =>
        value.Contains(',') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
}
