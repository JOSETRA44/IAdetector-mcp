using System.CommandLine;
using IAdetector.Cli.Rendering;
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

        var jsonOption = new Option<bool>("--json") { Description = "Salida en formato JSON." };
        var verboseOption = new Option<bool>("--verbose") { Description = "Incluye el desglose de cada señal." };
        var noColorOption = new Option<bool>("--no-color") { Description = "Desactiva colores en la salida." };

        var command = new Command("detect", "Analiza un texto o archivo y estima si fue generado por IA.")
        {
            fileArgument,
            textOption,
            jsonOption,
            verboseOption,
            noColorOption,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var file = parseResult.GetValue(fileArgument);
            var text = parseResult.GetValue(textOption);
            var json = parseResult.GetValue(jsonOption);
            var verbose = parseResult.GetValue(verboseOption);
            var noColor = parseResult.GetValue(noColorOption);

            if (file is null && string.IsNullOrEmpty(text))
            {
                Console.Error.WriteLine("Error: se requiere un archivo o --text.");
                return 1;
            }

            if (file is not null && !string.IsNullOrEmpty(text))
            {
                Console.Error.WriteLine("Error: especifica un archivo o --text, no ambos.");
                return 1;
            }

            string content;
            string label;

            if (file is not null)
            {
                if (!file.Exists)
                {
                    Console.Error.WriteLine($"Error: no se encontró el archivo '{file.FullName}'.");
                    return 1;
                }

                content = await File.ReadAllTextAsync(file.FullName, cancellationToken).ConfigureAwait(false);
                label = file.FullName;
            }
            else
            {
                content = text!;
                label = "<texto inline>";
            }

            var result = await detectText.DetectAsync(content, cancellationToken).ConfigureAwait(false);

            if (json)
            {
                ResultPrinter.PrintJson(result, verbose);
            }
            else
            {
                ResultPrinter.PrintHuman(label, result, verbose, noColor);
            }

            return 0;
        });

        return command;
    }
}
