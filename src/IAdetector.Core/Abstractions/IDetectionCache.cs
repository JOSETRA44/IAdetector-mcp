using IAdetector.Core.Domain;

namespace IAdetector.Core.Abstractions;

/// <summary>
/// Qué hacer con la caché de resultados en una invocación concreta. La detección es
/// determinista y cara (dos modelos ONNX por fragmento), así que reanalizar el mismo
/// documento no debería volver a pagar la inferencia.
/// </summary>
public enum DetectionCacheMode
{
    /// <summary>Lee la entrada cacheada si existe y escribe el resultado nuevo si no (por defecto).</summary>
    ReadWrite,

    /// <summary>Ignora lo cacheado pero reescribe la entrada con el resultado recién calculado (<c>--refresh</c>).</summary>
    Refresh,

    /// <summary>Ni lee ni escribe: la caché queda intacta (<c>--no-cache</c>).</summary>
    Disabled,
}

/// <summary>
/// Caché persistente entre procesos de resultados de detección. Una CLI termina tras una
/// sola llamada, así que una caché en memoria no le sirve de nada: la clave debe ser estable
/// entre ejecuciones y depender del texto y de la versión de los modelos/pesos que lo puntuaron.
/// </summary>
public interface IDetectionCache
{
    /// <summary>Devuelve el resultado cacheado para <paramref name="text"/>, o null si no hay acierto.</summary>
    Task<DetectionResult?> TryGetAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>Guarda el resultado. Nunca debe lanzar por problemas de disco: una caché rota degrada, no rompe.</summary>
    Task SetAsync(string text, DetectionResult result, CancellationToken cancellationToken = default);
}
