using System.CommandLine;
using IAdetector.Cli.Output;
using IAdetector.Core.Abstractions;
using IAdetector.Infrastructure.Ml;

namespace IAdetector.Cli.Commands;

internal static class ModelsCommand
{
    public static Command Create(IModelCache cache, IModelDownloader downloader)
    {
        return new Command("models", "Gestiona los modelos ONNX descargables.")
        {
            CreateList(cache),
            CreateDownload(cache, downloader),
        };
    }

    private static Command CreateList(IModelCache cache)
    {
        var command = new Command("list", "Lista los modelos del catálogo y si están descargados.")
            .WithGlobals();

        command.SetAction(parseResult => Guard.Run(parseResult, "models list", context =>
        {
            var renderer = new Renderer(context);

            foreach (var entry in ModelCatalog.Entries.Values)
            {
                var downloaded = cache.IsDownloaded(entry.ModelId);
                renderer.Add(new Row()
                    .Add("model", entry.ModelId)
                    .Add("state", downloaded ? "downloaded" : "missing")
                    .Add("path", downloaded ? cache.GetPath(entry.ModelId) : null)
                    .Add("display_name", entry.DisplayName));
            }

            renderer.Finish(new EnvelopeInfo("models list"));
            return ExitCodes.Ok;
        }));

        return command;
    }

    private static Command CreateDownload(IModelCache cache, IModelDownloader downloader)
    {
        var modelOption = new Option<string>("--model")
        {
            Description = "Id del modelo a descargar.",
            DefaultValueFactory = _ => ModelCatalog.RobertaOpenAiDetector,
        };

        var forceOption = new Option<bool>("--force")
        {
            Description = "Vuelve a descargar aunque ya exista.",
        };

        var command = new Command("download", "Descarga un modelo ONNX del catálogo.")
        {
            modelOption,
            forceOption,
        };

        command.WithGlobals();

        command.SetAction((parseResult, cancellationToken) =>
            Guard.RunAsync(parseResult, "models download", async context =>
        {
            var modelId = parseResult.GetValue(modelOption) ?? ModelCatalog.RobertaOpenAiDetector;
            var force = parseResult.GetValue(forceOption);
            var renderer = new Renderer(context);

            if (force && Directory.Exists(cache.GetPath(modelId)))
            {
                Directory.Delete(cache.GetPath(modelId), recursive: true);
            }
            else if (cache.IsDownloaded(modelId))
            {
                context.Note($"El modelo '{modelId}' ya está descargado.");
                renderer.Add(new Row()
                    .Add("model", modelId)
                    .Add("state", "downloaded")
                    .Add("path", cache.GetPath(modelId)));
                renderer.Finish(new EnvelopeInfo("models download") { Cached = true });
                return ExitCodes.Ok;
            }

            context.Note($"Descargando '{modelId}'...");

            // El progreso va por stderr: es lo único de este comando que no es dato, y en stdout
            // rompería un `models download -f json | jq`.
            var lastReported = -1;
            var progress = new Progress<double>(fraction =>
            {
                var percent = (int)(fraction * 100);
                if (percent != lastReported && !context.Quiet)
                {
                    lastReported = percent;
                    Console.Error.Write($"\r  {percent,3}%");
                }
            });

            var directory = await downloader
                .EnsureAvailableAsync(modelId, progress, cancellationToken)
                .ConfigureAwait(false);

            if (!context.Quiet)
            {
                Console.Error.WriteLine();
            }

            renderer.Add(new Row()
                .Add("model", modelId)
                .Add("state", "downloaded")
                .Add("path", directory));
            renderer.Finish(new EnvelopeInfo("models download"));
            return ExitCodes.Ok;
        }));

        return command;
    }
}
