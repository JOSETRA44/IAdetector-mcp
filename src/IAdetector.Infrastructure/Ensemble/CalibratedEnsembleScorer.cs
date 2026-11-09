using IAdetector.Core.Abstractions;
using IAdetector.Core.Domain;

namespace IAdetector.Infrastructure.Ensemble;

/// <summary>
/// Combina señales vía una regresión logística calibrada offline (<see cref="EnsembleWeightsData"/>):
/// <c>P(IA) = sigmoid(intercept + Σ peso[señal] * NormalizedScore)</c>. Reemplaza el promedio
/// ponderado fijo de la Fase 1 una vez calibrado sobre datos reales (ver
/// <c>scripts/calibration/fit_logistic_regression.py</c>).
/// </summary>
public sealed class CalibratedEnsembleScorer : IEnsembleScorer
{
    private readonly EnsembleWeightsData _weights;

    public CalibratedEnsembleScorer()
        : this(EnsembleWeightsResource.Load())
    {
    }

    /// <summary>Permite inyectar coeficientes propios (tests, o pesos recalibrados sin republicar el paquete).</summary>
    public CalibratedEnsembleScorer(EnsembleWeightsData weights)
    {
        _weights = weights;
    }

    public DetectionResult Combine(TextSample sample, IReadOnlyList<SignalScore> scores, ChunkInfo? chunkInfo = null)
    {
        if (scores.Count == 0 || scores.All(s => !s.HasData))
        {
            // Sin evidencia no hay veredicto: antes, con todas las señales en el 0.5
            // convencional de «sin datos» (modelos ONNX no descargados o texto demasiado
            // corto), el intercept calibrado producía ≈0.39 de probabilidad con ≈0.78 de
            // confianza — un texto vacío salía «probablemente humano» con confianza alta.
            return new DetectionResult(Verdict.Mixed, 0.5, 0.0, scores, chunkInfo);
        }

        var z = _weights.Intercept;
        foreach (var score in scores)
        {
            if (_weights.Weights.TryGetValue(score.Name, out var weight))
            {
                z += weight * score.NormalizedScore;
            }
        }

        var aiProbability = Sigmoid(z);
        var totalWeight = scores.Sum(s => s.Weight);
        var confidence = EnsembleMath.ComputeConfidence(scores, totalWeight);
        var verdict = EnsembleMath.DetermineVerdict(aiProbability);

        return new DetectionResult(verdict, aiProbability, confidence, scores, chunkInfo);
    }

    private static double Sigmoid(double z) => 1.0 / (1.0 + Math.Exp(-z));
}
