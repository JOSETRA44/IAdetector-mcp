using System.CommandLine;
using System.Text.Json;
using IAdetector.Cli.Commands;
using IAdetector.Cli.Output;

namespace IAdetector.Cli.Tests;

/// <summary>
/// El manifiesto de <c>describe --json</c> es lo único que un agente lee antes de invocar algo,
/// así que su exhaustividad se verifica contra el árbol real de comandos, no contra una lista.
/// </summary>
public sealed class ManifestTests
{
    /// <summary>Un árbol con la misma forma que el real, sin necesitar los modelos ONNX.</summary>
    private static RootCommand BuildTree()
    {
        var root = new RootCommand("raíz de prueba");

        var detect = new Command("detect", "Analiza un texto.");
        detect.Arguments.Add(new Argument<FileInfo?>("file") { Arity = ArgumentArity.ZeroOrOne });
        detect.Options.Add(new Option<bool>("--verbose") { Description = "Desglose por señal." });
        detect.WithGlobals();

        var batch = new Command("batch", "Analiza un directorio.");
        batch.Options.Add(new Option<int>("--max") { Description = "Tope de archivos." });
        batch.WithGlobals();

        var models = new Command("models", "Gestiona modelos.")
        {
            new Command("list", "Lista modelos.").WithGlobals(),
            new Command("download", "Descarga un modelo.").WithGlobals(),
            new Command("verify", "Verifica modelos.").WithGlobals(),
        };

        var cache = new Command("cache", "Caché de resultados.")
        {
            new Command("stats", "Estadísticas.").WithGlobals(),
            new Command("clear", "Vacía la caché.").WithGlobals(),
            new Command("path", "Ruta de la caché.").WithGlobals(),
        };

        root.Subcommands.Add(detect);
        root.Subcommands.Add(batch);
        root.Subcommands.Add(models);
        root.Subcommands.Add(cache);
        root.Subcommands.Add(DescribeCommand.Create(root, "1.0.0"));
        return root;
    }

    private static JsonElement Manifest()
    {
        var json = DescribeCommand.BuildManifest(BuildTree(), "1.0.0");
        Assert.NotNull(json);
        return JsonDocument.Parse(json!).RootElement.Clone();
    }

    [Fact]
    public void Trae_todas_las_claves_obligatorias()
    {
        var manifest = Manifest();
        foreach (var key in new[] { "name", "version", "source", "formats", "exit_codes", "commands" })
        {
            Assert.True(manifest.TryGetProperty(key, out _), $"falta '{key}'");
        }
    }

    [Fact]
    public void Declara_los_cinco_formatos()
    {
        var formats = Manifest().GetProperty("formats").EnumerateArray().Select(f => f.GetString()).ToList();
        Assert.Equal(["table", "json", "jsonl", "csv", "md"], formats);
    }

    [Fact]
    public void Documenta_los_siete_codigos_de_salida()
    {
        var codes = Manifest().GetProperty("exit_codes").EnumerateObject().Select(p => p.Name).Order().ToList();
        Assert.Equal(["0", "1", "2", "3", "4", "5", "6"], codes);
    }

    [Fact]
    public void Lista_cada_comando_hoja_del_arbol_real()
    {
        // Es la prueba que impide que el manifiesto se desactualice: si alguien agrega un
        // subcomando y no lo describe, esto falla en vez de que un agente lo descubra invocando.
        var described = Manifest()
            .GetProperty("commands")
            .EnumerateArray()
            .Select(c => c.GetProperty("name").GetString() ?? string.Empty)
            .ToHashSet(StringComparer.Ordinal);

        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            "detect", "batch", "models list", "models download", "models verify",
            "cache stats", "cache clear", "cache path", "describe",
        };

        Assert.Equal(expected, described);
    }

    [Fact]
    public void Cada_comando_trae_resumen_y_al_menos_un_ejemplo()
    {
        foreach (var command in Manifest().GetProperty("commands").EnumerateArray())
        {
            var name = command.GetProperty("name").GetString();
            Assert.False(string.IsNullOrWhiteSpace(command.GetProperty("summary").GetString()), name);
            Assert.True(command.GetProperty("examples").GetArrayLength() > 0, $"{name} no trae ejemplos");
        }
    }

    [Fact]
    public void Solo_models_download_se_declara_como_de_red()
    {
        // El resto se responde sin salir de la máquina; decirlo mal haría que un consumidor
        // esperase una caché donde no la hay, o al revés.
        var network = Manifest()
            .GetProperty("commands")
            .EnumerateArray()
            .Where(c => c.GetProperty("network").GetBoolean())
            .Select(c => c.GetProperty("name").GetString())
            .ToList();

        Assert.Equal(["models download"], network);
    }

    [Fact]
    public void Las_opciones_globales_se_listan_una_vez_arriba_y_no_en_cada_comando()
    {
        var manifest = Manifest();
        var globals = manifest
            .GetProperty("global_options")
            .EnumerateArray()
            .Select(o => o.GetProperty("name").GetString())
            .ToList();

        Assert.Contains("--format", globals);
        Assert.Contains("--refresh", globals);

        foreach (var command in manifest.GetProperty("commands").EnumerateArray())
        {
            var names = command.GetProperty("options").EnumerateArray().Select(o => o.GetProperty("name").GetString());
            Assert.DoesNotContain("--format", names);
        }
    }

    [Fact]
    public void El_alias_oculto_json_no_se_documenta()
    {
        // Sigue funcionando para scripts viejos, pero no debe aparecer como forma recomendada.
        var globals = Manifest()
            .GetProperty("global_options")
            .EnumerateArray()
            .Select(o => o.GetProperty("name").GetString());

        Assert.DoesNotContain("--json", globals);
    }

    [Fact]
    public void Pedir_un_comando_inexistente_devuelve_null()
    {
        Assert.Null(DescribeCommand.BuildManifest(BuildTree(), "1.0.0", "no-existe"));
    }

    [Fact]
    public void Pedir_un_comando_concreto_devuelve_solo_ese()
    {
        var json = DescribeCommand.BuildManifest(BuildTree(), "1.0.0", "detect");
        Assert.NotNull(json);
        var commands = JsonDocument.Parse(json!).RootElement.GetProperty("commands");
        Assert.Equal(1, commands.GetArrayLength());
        Assert.Equal("detect", commands[0].GetProperty("name").GetString());
    }
}
