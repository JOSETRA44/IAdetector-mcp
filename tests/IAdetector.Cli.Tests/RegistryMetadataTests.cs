using System.Text.Json;
using System.Text.RegularExpressions;

namespace IAdetector.Cli.Tests;

/// <summary>
/// Los metadatos del registro deben coincidir con lo que realmente se publica.
/// </summary>
/// <remarks>
/// El registro oficial de MCP prueba la propiedad del paquete comparando el marcador
/// <c>mcp-name</c> del README empaquetado contra el <c>name</c> de server.json, y rechaza un
/// envío cuya versión declarada no coincida con la publicada. Se rompe con sólo actualizar un
/// archivo y olvidar el otro, y el fallo aparece recién al publicar — así que se comprueba aquí.
/// </remarks>
public sealed class RegistryMetadataTests
{
    private static readonly string Root = FindRoot();
    private static readonly JsonElement Manifest = Load();

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "server.json")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("no se encontró server.json");
    }

    private static JsonElement Load() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "server.json"))).RootElement.Clone();

    private static string PackageVersion()
    {
        var csproj = File.ReadAllText(Path.Combine(Root, "src", "IAdetector.Cli", "IAdetector.Cli.csproj"));
        return Regex.Match(csproj, @"<PackageVersion>([^<]+)</PackageVersion>").Groups[1].Value;
    }

    [Fact]
    public void El_manifiesto_trae_las_claves_obligatorias()
    {
        foreach (var key in new[] { "$schema", "name", "version", "packages" })
        {
            Assert.True(Manifest.TryGetProperty(key, out _), $"falta '{key}'");
        }
    }

    [Fact]
    public void El_nombre_esta_en_formato_dns_inverso()
    {
        Assert.Matches(@"^io\.github\.[\w-]+/[\w.-]+$", Manifest.GetProperty("name").GetString()!);
    }

    [Fact]
    public void El_marcador_del_readme_coincide_con_el_nombre_del_manifiesto()
    {
        var readme = File.ReadAllText(Path.Combine(Root, "README.md"));
        Assert.Contains($"mcp-name: {Manifest.GetProperty("name").GetString()}", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void La_version_del_manifiesto_coincide_con_la_del_paquete()
    {
        Assert.Equal(PackageVersion(), Manifest.GetProperty("version").GetString());
        Assert.Equal(
            Manifest.GetProperty("version").GetString(),
            Manifest.GetProperty("packages")[0].GetProperty("version").GetString());
    }

    [Fact]
    public void La_registry_base_url_es_una_de_las_que_el_registro_acepta()
    {
        var package = Manifest.GetProperty("packages")[0];
        Assert.Equal("nuget", package.GetProperty("registryType").GetString());
        Assert.Equal("https://api.nuget.org/v3/index.json", package.GetProperty("registryBaseUrl").GetString());
    }

    [Fact]
    public void Existe_un_archivo_de_licencia()
    {
        // Los registros no listan un servidor sin licencia detectable.
        Assert.True(File.Exists(Path.Combine(Root, "LICENSE")));
    }
}
