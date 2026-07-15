using System.CommandLine;
using System.Text;
using IAdetector.Cli.Commands;
using IAdetector.Core.Abstractions;
using IAdetector.Core.UseCases;
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

var rootCommand = new RootCommand("iadetector: detecta texto generado por IA, 100% local (sin APIs externas).")
{
    DetectCommand.Create(detectText),
    BatchCommand.Create(batchDetect),
    ModelsCommand.Create(modelCache, modelDownloader),
};

return await rootCommand.Parse(args).InvokeAsync();
