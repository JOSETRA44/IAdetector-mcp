using System.CommandLine;
using IAdetector.Cli.Output;
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

        var recursiveOption = new Option<bool>("--recursive")
        {
            Description = "Buscar también en subdirectorios.",
        };

        var allOption = new Option<bool>("--all")
        {
            Description = "Analiza todos los archivos que coincidan, sin el tope por defecto de --max.",
        };

        var maxOption = new Option<int>("--max")
        {
            Description = "Tope de archivos a procesar (evita quedarse horas en un directorio enorme por error).",
            DefaultValueFactory = _ => 1000,
        };

        var parallelismOption = new Option<int>("--parallelism")
        {
            Description = "Detecciones simultáneas (por defecto 4).",
            DefaultValueFactory = _ => 4,
        };

        var verboseOption = new Option<bool>("--verbose")
        {
            Description = "Incluye el desglose de señales por archivo (una columna por señal).",
        };

        var command = new Command("batch", "Analiza todos los archivos de texto de un directorio.")
        {
            directoryArgument,
            patternOption,
            recursiveOption,
            allOption,
            maxOption,
            parallelismOption,
            verboseOption,
        };

        command.WithGlobals();

        command.SetAction((parseResult, cancellationToken) =>
            Guard.RunAsync(parseResult, "batch", async context =>
        {
            var directory = parseResult.GetValue(directoryArgument);
            var pattern = parseResult.GetValue(patternOption) ?? "*.txt";
            var recursive = parseResult.GetValue(recursiveOption);
            var all = parseResult.GetValue(allOption);
            var max = parseResult.GetValue(maxOption);
            var parallelism = Math.Max(1, parseResult.GetValue(parallelismOption));
            var verbose = parseResult.GetValue(verboseOption);

            if (directory is null || !directory.Exists)
            {
                throw CliException.NotFound(
                    $"No se encontró el directorio '{directory?.FullName}'.",
                    "Verifica la ruta, o usa `iadetector detect` para un solo archivo.");
            }

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var matches = Directory.EnumerateFiles(directory.FullName, pattern, searchOption);
            var files = (all ? matches : matches.Take(max)).ToList();

            var renderer = new Renderer(context);

            if (files.Count == 0)
            {
                // Un conjunto vacío no es un error: exit 0, count 0 (contrato, sección 2).
                context.Note($"Ningún archivo coincide con '{pattern}' en {directory.FullName}.");
                renderer.Finish(new EnvelopeInfo("batch")
                {
                    Meta = [new("pattern", Cell.Of(pattern)), new("files", Cell.Of(0L))],
                });
                return ExitCodes.Ok;
            }

            var succeeded = 0;
            var failed = 0;
            var cachedCount = 0;
            var done = 0;

            // Se consume el streaming, no DetectFilesAsync: en jsonl/csv las filas deben ir
            // saliendo mientras corre. Con 500 documentos, bufferizar todo son minutos a ciegas.
            await foreach (var entry in batchDetect
                .DetectFilesStreamingAsync(files, parallelism, context.CacheMode, cancellationToken)
                .ConfigureAwait(false))
            {
                if (entry.Result is null)
                {
                    failed++;
                }
                else
                {
                    succeeded++;
                    if (entry.Cached)
                    {
                        cachedCount++;
                    }
                }

                renderer.Add(Projections.BatchRow(entry, verbose));

                done++;
                if (done % 10 == 0 || done == files.Count)
                {
                    context.Note($"Analizados {done}/{files.Count}...");
                }
            }

            context.Note($"{succeeded} correcto(s), {failed} con error, {cachedCount} desde caché.");

            renderer.Finish(new EnvelopeInfo("batch")
            {
                // El lote entero cuenta como cacheado sólo si nada hubo que recalcular.
                Cached = failed == 0 && cachedCount == succeeded && succeeded > 0,
                Meta =
                [
                    new("pattern", Cell.Of(pattern)),
                    new("files", Cell.Of((long)files.Count)),
                    new("succeeded", Cell.Of((long)succeeded)),
                    new("failed", Cell.Of((long)failed)),
                    new("from_cache", Cell.Of((long)cachedCount)),
                    new("parallelism", Cell.Of((long)parallelism)),
                ],
            });

            // Que todos fallaran sí es un fallo, aunque cada fila lo explique por separado.
            return succeeded == 0 && failed > 0 ? ExitCodes.Failure : ExitCodes.Ok;
        }));

        return command;
    }
}
