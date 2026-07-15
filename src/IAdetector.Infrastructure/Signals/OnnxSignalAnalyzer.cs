using IAdetector.Core.Abstractions;
using IAdetector.Core.Domain;

namespace IAdetector.Infrastructure.Signals;

/// <summary>
/// Adapta el clasificador ONNX (<see cref="IOnnxClassifier"/>) al contrato de
/// <see cref="ISignalAnalyzer"/> para que participe en el mismo ensemble que las señales
/// estadísticas. Si el modelo no está descargado, no descarga nada automáticamente (ver
/// <see cref="IModelDownloader"/>/comando <c>models download</c>): reporta neutral con peso
/// reducido en su lugar, igual que las demás señales cuando les falta información.
/// </summary>
public sealed class OnnxSignalAnalyzer(IModelCache cache, IOnnxClassifier classifier) : ISignalAnalyzer
{
    public string Name => "onnx-classifier";

    // Un clasificador supervisado entrenado es, en principio, una señal más confiable que las
    // heurísticas estadísticas puras; peso provisional, sujeto a la calibración de la Fase 3.
    private const double Weight = 2.0;

    public async Task<SignalScore> AnalyzeAsync(TextSample sample, CancellationToken cancellationToken = default)
    {
        if (!cache.IsDownloaded(classifier.ModelId))
        {
            return new SignalScore(
                Name, 0.0, 0.5, Weight * ScoreNormalization.InsufficientDataWeightFactor,
                $"Modelo '{classifier.ModelId}' no descargado. Ejecuta 'iadetector models download' para activar esta señal.");
        }

        var aiProbability = await classifier.ClassifyAsync(sample.Text, cancellationToken).ConfigureAwait(false);

        return new SignalScore(
            Name, aiProbability, aiProbability, Weight,
            $"Clasificador RoBERTa: probabilidad de IA={aiProbability:P0}.");
    }
}
