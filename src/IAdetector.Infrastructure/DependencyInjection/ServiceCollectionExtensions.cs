using IAdetector.Core.Abstractions;
using IAdetector.Core.UseCases;
using IAdetector.Infrastructure.Ensemble;
using IAdetector.Infrastructure.Ml;
using IAdetector.Infrastructure.Signals;
using Microsoft.Extensions.DependencyInjection;

namespace IAdetector.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra las señales estadísticas, el clasificador ONNX (Fase 2), el ensemble y los casos
    /// de uso. El clasificador ONNX siempre se registra como señal, pero solo aporta una
    /// probabilidad real cuando su modelo ya fue descargado (<see cref="IModelCache"/>); si no,
    /// se comporta como cualquier otra señal con datos insuficientes.
    /// </summary>
    public static IServiceCollection AddIAdetectorInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ISignalAnalyzer, BurstinessAnalyzer>();
        services.AddSingleton<ISignalAnalyzer, TypeTokenRatioAnalyzer>();
        services.AddSingleton<ISignalAnalyzer, EntropyAnalyzer>();
        services.AddSingleton<ISignalAnalyzer, CompressionRatioAnalyzer>();
        services.AddSingleton<ISignalAnalyzer, NGramRepetitionAnalyzer>();

        services.AddSingleton<IEnsembleScorer, WeightedEnsembleScorer>();

        services.AddSingleton<IModelCache, FileSystemModelCache>();
        services.AddSingleton<IModelDownloader, HttpModelDownloader>();
        services.AddSingleton<IOnnxClassifier>(sp => new RobertaOnnxClassifier(sp.GetRequiredService<IModelCache>()));
        services.AddSingleton<ISignalAnalyzer, OnnxSignalAnalyzer>();

        services.AddSingleton<DetectTextUseCase>();
        services.AddSingleton<BatchDetectUseCase>();

        return services;
    }
}
