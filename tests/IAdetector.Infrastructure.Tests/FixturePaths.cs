namespace IAdetector.Infrastructure.Tests;

/// <summary>
/// Resuelve rutas a tests/Fixtures desde el directorio de salida del test (bin/Debug/net10.0/…),
/// leyendo directamente del árbol de fuentes en vez de copiar los archivos en cada build.
/// </summary>
internal static class FixturePaths
{
    private static readonly string Root = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Fixtures"));

    public static string Human(string fileName) => Path.Combine(Root, "human", fileName);

    public static string Ai(string fileName) => Path.Combine(Root, "ai", fileName);
}
