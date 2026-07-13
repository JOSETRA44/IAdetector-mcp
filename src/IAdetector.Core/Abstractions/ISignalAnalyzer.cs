using IAdetector.Core.Domain;

namespace IAdetector.Core.Abstractions;

/// <summary>
/// Una señal individual de detección (estadística, estilométrica o basada en modelo).
/// Las implementaciones deben ser tolerantes a textos cortos o degenerados: nunca lanzar
/// por falta de datos, sino devolver un <see cref="SignalScore"/> neutral con explicación.
/// </summary>
public interface ISignalAnalyzer
{
    /// <summary>Identificador estable de la señal (ej. "burstiness"). Usado como <see cref="SignalScore.Name"/>.</summary>
    string Name { get; }

    Task<SignalScore> AnalyzeAsync(TextSample sample, CancellationToken cancellationToken = default);
}
