using System.CommandLine;
using IAdetector.Core.Abstractions;
using IAdetector.Infrastructure.Ml;

namespace IAdetector.Cli.Commands;

internal static class ModelsCommand
{
    public static Command Create(IModelCache cache, IModelDownloader downloader)
    {
        var modelOption = new Option<string>("--model")
        {
            Description = "Id del modelo a descargar.",
            DefaultValueFactory = _ => ModelCatalog.RobertaOpenAiDetector,
        };
        var forceOption = new Option<bool>("--force") { Description = "Vuelve a descargar aunque ya exista." };

        var downloadCommand = new Command("download", "Descarga un modelo ONNX del catálogo.")
        {
            modelOption,
            forceOption,
        };

        downloadCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var modelId = parseResult.GetValue(modelOption) ?? ModelCatalog.RobertaOpenAiDetector;
            var force = parseResult.GetValue(forceOption);

            if (force)
            {
                var existingDir = cache.GetPath(modelId);
                if (Directory.Exists(existingDir))
                {
                    Directory.Delete(existingDir, recursive: true);
                }
            }
            else if (cache.IsDownloaded(modelId))
            {
                Console.WriteLine($"El modelo '{modelId}' ya está descargado.");
                return 0;
            }

            Console.WriteLine($"Descargando '{modelId}'...");
            var lastReported = -1;
            var progress = new Progress<double>(p =>
            {
                var pct = (int)(p * 100);
                if (pct == lastReported)
                {
                    return;
                }

                lastReported = pct;
                Console.Write($"\r  {pct,3}%");
            });

            try
            {
                var dir = await downloader.EnsureAvailableAsync(modelId, progress, cancellationToken).ConfigureAwait(false);
                Console.WriteLine();
                Console.WriteLine($"Modelo descargado en: {dir}");
                return 0;
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or IOException)
            {
                Console.WriteLine();
                Console.Error.WriteLine($"Error al descargar el modelo: {ex.Message}");
                return 1;
            }
        });

        var listCommand = new Command("list", "Lista los modelos del catálogo y si están descargados.");
        listCommand.SetAction(_ =>
        {
            foreach (var entry in ModelCatalog.Entries.Values)
            {
                var status = cache.IsDownloaded(entry.ModelId) ? "descargado" : "no descargado";
                Console.WriteLine($"{entry.ModelId,-25} {status,-15} {entry.DisplayName}");
            }
        });

        return new Command("models", "Gestiona los modelos ONNX descargables.")
        {
            downloadCommand,
            listCommand,
        };
    }
}
