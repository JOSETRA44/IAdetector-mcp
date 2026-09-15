using System.CommandLine;
using IAdetector.Cli.Output;
using IAdetector.Core.UseCases;

namespace IAdetector.Cli.Commands;

internal static class DetectCommand
{
    public static Command Create(DetectTextUseCase detectText)
    {
        var fileArgument = new Argument<FileInfo?>("file")
        {
            Description = "Archivo de texto a analizar.",
            Arity = ArgumentArity.ZeroOrOne,
        };

        var textOption = new Option<string?>("--text")
        {
            Description = "Texto a analizar directamente, en lugar de un archivo.",
        };

        var verboseOption = new Option<bool>("--verbose")
        {
            Description = "Incluye el desglose de cada señal (una columna por señal).",
        };

        var command = new Command(
            "detect",
            "Analiza un texto o archivo y estima si fue generado por IA. Sin archivo ni --text, lee de stdin.")
        {
            fileArgument,
            textOption,
            verboseOption,
        };

        command.WithGlobals();

        command.SetAction((parseResult, cancellationToken) =>
            Guard.RunAsync(parseResult, "detect", async context =>
        {
            var file = parseResult.GetValue(fileArgument);
            var text = parseResult.GetValue(textOption);
            var verbose = parseResult.GetValue(verboseOption);

            var (content, label) = await ReadInputAsync(file, text, cancellationToken).ConfigureAwait(false);

            var outcome = await detectText
                .DetectWithCacheAsync(content, context.CacheMode, cancellationToken)
                .ConfigureAwait(false);

            var renderer = new Renderer(context);
            renderer.Add(Projections.DetectionRow(label, outcome, verbose));
            renderer.Finish(
                new EnvelopeInfo("detect")
                {
                    Cached = outcome.Cached,
                    Meta = [new("verbose", Cell.Of(verbose)), new("source_label", Cell.Of(label))],
                    WriteData = writer => Projections.WriteDetection(writer, outcome.Result, verbose),
                },
                Projections.HumanDetail(label, outcome.Result, outcome.TotalWords, outcome.Cached, verbose));

            return ExitCodes.Ok;
        }));

        return command;
    }

    /// <summary>Resuelve de dónde sale el texto, en orden: archivo, --text, stdin.</summary>
    private static async Task<(string Content, string Label)> ReadInputAsync(
        FileInfo? file,
        string? text,
        CancellationToken cancellationToken)
    {
        if (file is not null && !string.IsNullOrEmpty(text))
        {
            throw CliException.Usage(
                "Se especificaron a la vez un archivo y --text.",
                "Usa uno u otro: `iadetector detect archivo.txt` o `iadetector detect --text \"...\"`.");
        }

        if (file is not null)
        {
            if (!file.Exists)
            {
                throw CliException.NotFound(
                    $"No se encontró el archivo '{file.FullName}'.",
                    "Verifica la ruta, o pasa el texto con --text.");
            }

            var content = await File.ReadAllTextAsync(file.FullName, cancellationToken).ConfigureAwait(false);
            return (content, file.FullName);
        }

        if (!string.IsNullOrEmpty(text))
        {
            return (text, "<texto inline>");
        }

        if (Console.IsInputRedirected)
        {
            var content = await Console.In.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            return (content, "<stdin>");
        }

        throw CliException.Usage(
            "No se recibió texto que analizar.",
            "Pasa un archivo, usa --text \"...\", o redirige por stdin (`cat x.txt | iadetector detect`).");
    }
}
