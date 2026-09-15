using System.CommandLine;
using IAdetector.Cli.Output;
using IAdetector.Infrastructure.Caching;

namespace IAdetector.Cli.Commands;

/// <summary>
/// <c>cache stats|clear|path</c> — inspecciona o vacía la caché de resultados.
/// </summary>
/// <remarks>
/// Aquí no hay caché HTTP que administrar: lo que se guarda es el resultado de la inferencia,
/// que es lo caro. Reanalizar un corpus de quinientos documentos por segunda vez debería ser
/// instantáneo en vez de volver a correr dos modelos ONNX por fragmento.
/// </remarks>
internal static class CacheCommand
{
    public static Command Create(FileSystemDetectionCache cache)
    {
        var command = new Command("cache", "Inspecciona o vacía la caché de resultados de detección.")
        {
            Action("stats", "Entradas, tamaño en disco y entradas obsoletas.", cache),
            Action("clear", "Borra todas las entradas.", cache),
            Action("path", "Imprime el directorio de la caché.", cache),
        };

        return command;
    }

    private static Command Action(string name, string description, FileSystemDetectionCache cache)
    {
        var command = new Command(name, description).WithGlobals();

        command.SetAction(parseResult => Guard.Run(parseResult, $"cache {name}", context =>
        {
            var renderer = new Renderer(context);
            var row = new Row().Add("path", cache.RootPath);

            switch (name)
            {
                case "clear":
                    row.Add("removed_entries", cache.Clear());
                    break;
                case "stats":
                    var stats = cache.GetStats();
                    row.Add("entries", stats.Entries)
                        .Add("stale_entries", stats.StaleEntries)
                        .Add("size_bytes", stats.SizeBytes);
                    break;
            }

            // La versión de modelos/pesos forma parte de la clave: si cambia, todo lo guardado
            // queda obsoleto de golpe. Verlo evita el desconcierto de una caché que "no acierta".
            row.Add("version_tag", cache.VersionTag);

            renderer.Add(row);
            renderer.Finish(new EnvelopeInfo($"cache {name}"));
            return ExitCodes.Ok;
        }));

        return command;
    }
}
