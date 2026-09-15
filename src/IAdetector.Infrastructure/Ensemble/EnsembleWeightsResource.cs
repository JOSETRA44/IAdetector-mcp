using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using IAdetector.Infrastructure.Serialization;

namespace IAdetector.Infrastructure.Ensemble;

/// <summary>
/// Acceso al <c>EnsembleWeights.json</c> embebido y a su huella de versión. La huella la usa la
/// caché de resultados: si se republican pesos recalibrados, todas las entradas anteriores dejan
/// de coincidir automáticamente en vez de devolver veredictos calculados con la calibración vieja.
/// </summary>
public static class EnsembleWeightsResource
{
    private const string EmbeddedResourceName = "IAdetector.Infrastructure.Ensemble.EnsembleWeights.json";

    private static readonly Lazy<string> LazyVersion = new(ComputeVersion, isThreadSafe: true);

    /// <summary>Primeros 16 hex del SHA-256 del recurso embebido: identifica la calibración vigente.</summary>
    public static string Version => LazyVersion.Value;

    public static EnsembleWeightsData Load()
    {
        using var stream = OpenStream();
        return JsonSerializer.Deserialize(stream, IAdetectorJsonContext.Default.EnsembleWeightsData)
            ?? throw new InvalidOperationException("EnsembleWeights.json embebido vacío o inválido.");
    }

    private static Stream OpenStream() =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException($"No se encontró el recurso embebido '{EmbeddedResourceName}'.");

    private static string ComputeVersion()
    {
        using var stream = OpenStream();
        return Convert.ToHexString(SHA256.HashData(stream))[..16].ToLowerInvariant();
    }
}
