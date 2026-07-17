namespace IAdetector.Infrastructure.Ensemble;

/// <summary>
/// Coeficientes de una regresión logística: <c>P(IA) = sigmoid(Intercept + Σ Weights[señal] * NormalizedScore)</c>.
/// Generado offline por <c>scripts/calibration/fit_logistic_regression.py</c> a partir de
/// <c>tools/IAdetector.Calibration</c>; ver <see cref="CalibratedEnsembleScorer"/>.
/// </summary>
public sealed record EnsembleWeightsData(double Intercept, Dictionary<string, double> Weights);
