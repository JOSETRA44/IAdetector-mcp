using IAdetector.Core.Domain;

namespace IAdetector.Core.Abstractions;

/// <summary>
/// Convierte un <see cref="DetectionResult"/> en una <see cref="DetectionExplanation"/>
/// accionable: contribuciones reales al resultado, comparación con los rangos humanos de
/// referencia y guía de reescritura por señal. Lo implementa Infrastructure porque
/// necesita los coeficientes calibrados y los rangos de referencia del corpus.
/// </summary>
public interface IDetectionExplainer
{
    DetectionExplanation Explain(DetectionResult result);
}
