using IAdetector.Core.Abstractions;
using IAdetector.Core.UseCases;
using IAdetector.Infrastructure.Ensemble;
using IAdetector.Infrastructure.Signals;
using Microsoft.Extensions.DependencyInjection;

namespace IAdetector.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra las señales estadísticas, el ensemble y los casos de uso. Los servicios basados en
    /// ONNX (Fases 2 y 4) se añaden condicionalmente cuando el modelo correspondiente está
    /// descargado; ver <c>AddOnnxSignals</c> una vez implementado en la Fase 2.
    /// </summary>
    public static IServiceCollection AddIAdetectorInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ISignalAnalyzer, BurstinessAnalyzer>();
        services.AddSingleton<ISignalAnalyzer, TypeTokenRatioAnalyzer>();
        services.AddSingleton<ISignalAnalyzer, EntropyAnalyzer>();
        services.AddSingleton<ISignalAnalyzer, CompressionRatioAnalyzer>();
        services.AddSingleton<ISignalAnalyzer, NGramRepetitionAnalyzer>();

        services.AddSingleton<IEnsembleScorer, WeightedEnsembleScorer>();

        services.AddSingleton<DetectTextUseCase>();
        services.AddSingleton<BatchDetectUseCase>();

        return services;
    }
}
