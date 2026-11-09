using IAdetector.Core.Abstractions;
using IAdetector.Core.UseCases;
using IAdetector.Infrastructure.Caching;
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

        services.AddSingleton<IEnsembleScorer, CalibratedEnsembleScorer>();

        services.AddSingleton<IModelCache, FileSystemModelCache>();

        // La caché de resultados se registra también por su tipo concreto porque el comando
        // `cache stats|clear|path` necesita administrarla, no solo consultarla.
        services.AddSingleton<FileSystemDetectionCache>();
        services.AddSingleton<IDetectionCache>(sp => sp.GetRequiredService<FileSystemDetectionCache>());
        services.AddSingleton<IModelDownloader, HttpModelDownloader>();
        services.AddSingleton<IOnnxClassifier>(sp => new RobertaOnnxClassifier(sp.GetRequiredService<IModelCache>()));
        services.AddSingleton<ISignalAnalyzer, OnnxSignalAnalyzer>();
        services.AddSingleton<ISignalAnalyzer, DistilGpt2PerplexityAnalyzer>();

        services.AddSingleton<DetectTextUseCase>();
        services.AddSingleton<BatchDetectUseCase>();
        services.AddSingleton<IDetectionExplainer, DetectionExplainer>();
        services.AddSingleton<CompareVersionsUseCase>();

        return services;
    }
}
