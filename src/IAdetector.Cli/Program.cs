using System.CommandLine;
using IAdetector.Cli.Commands;
using IAdetector.Core.UseCases;
using IAdetector.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddIAdetectorInfrastructure();
using var provider = services.BuildServiceProvider();

var detectText = provider.GetRequiredService<DetectTextUseCase>();
var batchDetect = provider.GetRequiredService<BatchDetectUseCase>();

var rootCommand = new RootCommand("iadetector: detecta texto generado por IA, 100% local (sin APIs externas).")
{
    DetectCommand.Create(detectText),
    BatchCommand.Create(batchDetect),
};

return await rootCommand.Parse(args).InvokeAsync();
