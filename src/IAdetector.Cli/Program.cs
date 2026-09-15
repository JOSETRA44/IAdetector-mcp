using System.CommandLine;
using System.Reflection;
using System.Text;
using IAdetector.Cli.Commands;
using IAdetector.Cli.Output;
using IAdetector.Core.Abstractions;
using IAdetector.Core.UseCases;
using IAdetector.Infrastructure.Caching;
using IAdetector.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

// Sin esto, binarios Native AOT en consolas Windows con code page no-UTF8 muestran acentos/ñ mal.
Console.OutputEncoding = Encoding.UTF8;

var services = new ServiceCollection();
services.AddIAdetectorInfrastructure();
using var provider = services.BuildServiceProvider();

var detectText = provider.GetRequiredService<DetectTextUseCase>();
var batchDetect = provider.GetRequiredService<BatchDetectUseCase>();
var modelCache = provider.GetRequiredService<IModelCache>();
var modelDownloader = provider.GetRequiredService<IModelDownloader>();
var detectionCache = provider.GetRequiredService<FileSystemDetectionCache>();

var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

var rootCommand = new RootCommand("iadetector: detecta texto generado por IA, 100% local (sin APIs externas).")
{
    DetectCommand.Create(detectText),
    BatchCommand.Create(batchDetect),
    ModelsCommand.Create(modelCache, modelDownloader),
    CacheCommand.Create(detectionCache),
};

// `describe` se construye al final porque recorre el árbol ya armado: derivar el manifiesto
// de una lista escrita a mano se desactualiza en el primer flag que alguien agregue.
rootCommand.Subcommands.Add(DescribeCommand.Create(rootCommand, version));

try
{
    return await rootCommand.Parse(args).InvokeAsync();
}
catch (CliException exception)
{
    // Único punto donde un fallo se vuelve sobre de error + código de salida, para que los
    // comandos no repitan el par "imprimir en stderr / devolver N".
    return Renderer.WriteError(ResolveContextSafely(args), CommandNameOf(args), exception.Error);
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Interrumpido.");
    return 130;
}

// Si el fallo fue al parsear los propios flags globales, no se puede confiar en ellos: se
// reporta en texto plano antes que arriesgar una segunda excepción mientras se reporta la primera.
static OutputContext ResolveContextSafely(string[] args)
{
    var wantsJson = args.Contains("--json", StringComparer.Ordinal)
        || IsFormat(args, "json")
        || IsFormat(args, "jsonl");

    return new OutputContext(
        wantsJson ? OutputFormat.Json : OutputFormat.Table,
        OutputFile: null,
        Quiet: args.Contains("--quiet", StringComparer.Ordinal) || args.Contains("-q", StringComparer.Ordinal),
        NoColor: true,
        CacheMode: DetectionCacheMode.ReadWrite);
}

static bool IsFormat(string[] args, string value)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if ((args[i] is "--format" or "-f") && string.Equals(args[i + 1], value, StringComparison.Ordinal))
        {
            return true;
        }
    }

    return args.Contains("--format=" + value, StringComparer.Ordinal);
}

static string CommandNameOf(string[] args) =>
    args.FirstOrDefault(a => !a.StartsWith('-')) ?? "iadetector";
