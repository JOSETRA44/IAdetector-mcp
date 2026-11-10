using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using IAdetector.Infrastructure.Serialization;

namespace IAdetector.Infrastructure.Ensemble;

/// <summary>
/// Acceso a los perfiles de calibración embebidos (inglés y español) y a su huella de
/// versión. La huella — calculada sobre AMBOS perfiles — la usa la caché de resultados:
/// si se republica cualquier calibración, todas las entradas anteriores dejan de
/// coincidir automáticamente en vez de devolver veredictos calculados con pesos viejos.
/// </summary>
public static class EnsembleWeightsResource
{
    private const string EnglishResourceName = "IAdetector.Infrastructure.Ensemble.EnsembleWeights.json";
    private const string SpanishResourceName = "IAdetector.Infrastructure.Ensemble.EnsembleWeightsEs.json";

    private static readonly Lazy<string> LazyVersion = new(ComputeVersion, isThreadSafe: true);

    /// <summary>Primeros 16 hex del SHA-256 combinado de ambos perfiles: identifica la calibración vigente.</summary>
    public static string Version => LazyVersion.Value;

    /// <summary>Perfil calibrado sobre HC3 (inglés).</summary>
    public static EnsembleWeightsData Load() => LoadResource(EnglishResourceName);

    /// <summary>Perfil calibrado sobre el corpus en español de la Fase 3.2 (ver docs/SPANISH.md).</summary>
    public static EnsembleWeightsData LoadSpanish() => LoadResource(SpanishResourceName);

    private static EnsembleWeightsData LoadResource(string resourceName)
    {
        using var stream = OpenStream(resourceName);
        return JsonSerializer.Deserialize(stream, IAdetectorJsonContext.Default.EnsembleWeightsData)
            ?? throw new InvalidOperationException($"{resourceName} embebido vacío o inválido.");
    }

    private static Stream OpenStream(string resourceName) =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"No se encontró el recurso embebido '{resourceName}'.");

    private static string ComputeVersion()
    {
        using var english = OpenStream(EnglishResourceName);
        using var spanish = OpenStream(SpanishResourceName);
        using var combined = new MemoryStream();
        english.CopyTo(combined);
        spanish.CopyTo(combined);
        return Convert.ToHexString(SHA256.HashData(combined.ToArray()))[..16].ToLowerInvariant();
    }
}
