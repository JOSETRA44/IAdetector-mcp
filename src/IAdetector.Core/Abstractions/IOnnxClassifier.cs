namespace IAdetector.Core.Abstractions;

/// <summary>
/// Aísla el dominio de la dependencia concreta en ONNX Runtime. Implementado en
/// IAdetector.Infrastructure a partir de la Fase 2 (clasificador RoBERTa) y reutilizado
/// en la Fase 4 (señal de perplexity).
/// </summary>
public interface IOnnxClassifier
{
    /// <summary>Identificador del modelo que esta instancia sirve (ver <see cref="IModelCache"/>).</summary>
    string ModelId { get; }

    /// <summary>Probabilidad [0,1] de que <paramref name="text"/> haya sido generado por IA.</summary>
    Task<float> ClassifyAsync(string text, CancellationToken cancellationToken = default);
}
