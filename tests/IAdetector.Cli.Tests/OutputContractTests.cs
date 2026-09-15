using System.Text.Json;
using IAdetector.Cli.Output;
using IAdetector.Core.Abstractions;

namespace IAdetector.Cli.Tests;

/// <summary>
/// El contrato Arsenal, verificado sobre la capa de salida.
/// </summary>
/// <remarks>
/// Ninguna prueba corre inferencia ONNX: lo que se ejercita es el sobre, los formatos y el
/// mapeo de errores, que es donde vive el contrato. La detección real ya la cubren
/// <c>IAdetector.Integration.Tests</c>.
/// </remarks>
public sealed class OutputContractTests
{
    private static OutputContext Context(OutputFormat format, FileInfo? output = null) =>
        new(format, output, Quiet: true, NoColor: true, DetectionCacheMode.ReadWrite);

    private static Row SampleRow(string path = "a.txt") => new Row()
        .Add("path", path)
        .Add("verdict", "likely_ai")
        .Add("ai_probability", 0.8123)
        .Add("total_words", 1200L)
        .Add("cached", false);

    private static string Capture(Action action)
    {
        var original = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            action();
        }
        finally
        {
            Console.SetOut(original);
        }

        return writer.ToString();
    }

    private static string CaptureError(Action action)
    {
        var original = Console.Error;
        using var writer = new StringWriter();
        Console.SetError(writer);
        try
        {
            action();
        }
        finally
        {
            Console.SetError(original);
        }

        return writer.ToString();
    }

    // --- sobre --------------------------------------------------------------

    [Fact]
    public void El_sobre_json_trae_todas_las_claves_obligatorias()
    {
        var output = Capture(() =>
        {
            var renderer = new Renderer(Context(OutputFormat.Json));
            renderer.Add(SampleRow());
            renderer.Finish(new EnvelopeInfo("detect"));
        });

        using var document = JsonDocument.Parse(output);
        var root = document.RootElement;

        foreach (var key in new[] { "ok", "command", "source", "fetched_at", "cached", "count", "meta", "data", "rows" })
        {
            Assert.True(root.TryGetProperty(key, out _), $"falta la clave '{key}'");
        }

        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.Equal("iadetector", root.GetProperty("source").GetString());
        Assert.Equal("detect", root.GetProperty("command").GetString());
    }

    [Fact]
    public void Count_siempre_coincide_con_el_numero_de_filas()
    {
        foreach (var count in new[] { 0, 1, 5 })
        {
            var output = Capture(() =>
            {
                var renderer = new Renderer(Context(OutputFormat.Json));
                for (var i = 0; i < count; i++)
                {
                    renderer.Add(SampleRow($"doc{i}.txt"));
                }

                renderer.Finish(new EnvelopeInfo("batch"));
            });

            using var document = JsonDocument.Parse(output);
            Assert.Equal(count, document.RootElement.GetProperty("count").GetInt32());
            Assert.Equal(count, document.RootElement.GetProperty("rows").GetArrayLength());
        }
    }

    [Fact]
    public void Los_numeros_salen_como_numeros_no_como_texto()
    {
        // Si salieran entrecomillados, un `jq 'select(.ai_probability > 0.8)'` — el uso típico
        // de un agente filtrando un corpus — dejaría de funcionar en silencio.
        var output = Capture(() =>
        {
            var renderer = new Renderer(Context(OutputFormat.Json));
            renderer.Add(SampleRow());
            renderer.Finish(new EnvelopeInfo("detect"));
        });

        using var document = JsonDocument.Parse(output);
        var row = document.RootElement.GetProperty("rows")[0];
        Assert.Equal(JsonValueKind.Number, row.GetProperty("ai_probability").ValueKind);
        Assert.Equal(JsonValueKind.Number, row.GetProperty("total_words").ValueKind);
        Assert.Equal(JsonValueKind.False, row.GetProperty("cached").ValueKind);
    }

    [Fact]
    public void El_meta_del_sobre_conserva_los_tipos()
    {
        var output = Capture(() =>
        {
            var renderer = new Renderer(Context(OutputFormat.Json));
            renderer.Finish(new EnvelopeInfo("batch")
            {
                Meta = [new("files", Cell.Of(12L)), new("pattern", Cell.Of("*.txt"))],
            });
        });

        using var document = JsonDocument.Parse(output);
        var meta = document.RootElement.GetProperty("meta");
        Assert.Equal(12, meta.GetProperty("files").GetInt32());
        Assert.Equal("*.txt", meta.GetProperty("pattern").GetString());
    }

    // --- formatos -----------------------------------------------------------

    [Fact]
    public void Jsonl_emite_un_objeto_por_linea()
    {
        var output = Capture(() =>
        {
            var renderer = new Renderer(Context(OutputFormat.Jsonl));
            renderer.Add(SampleRow("a.txt"));
            renderer.Add(SampleRow("b.txt"));
            renderer.Finish(new EnvelopeInfo("batch"));
        });

        var lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        foreach (var line in lines)
        {
            using var document = JsonDocument.Parse(line);
            Assert.True(document.RootElement.TryGetProperty("path", out _));
        }
    }

    [Fact]
    public void Csv_escribe_la_cabecera_una_sola_vez()
    {
        var output = Capture(() =>
        {
            var renderer = new Renderer(Context(OutputFormat.Csv));
            renderer.Add(SampleRow("a.txt"));
            renderer.Add(SampleRow("b.txt"));
            renderer.Finish(new EnvelopeInfo("batch"));
        });

        var lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.StartsWith("path,verdict,ai_probability", lines[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Csv_entrecomilla_los_valores_con_coma()
    {
        var output = Capture(() =>
        {
            var renderer = new Renderer(Context(OutputFormat.Csv));
            renderer.Add(new Row().Add("nota", "uno, dos"));
            renderer.Finish(new EnvelopeInfo("detect"));
        });

        Assert.Contains("\"uno, dos\"", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Markdown_escapa_las_barras_para_no_partir_la_tabla()
    {
        var output = Capture(() =>
        {
            var renderer = new Renderer(Context(OutputFormat.Md));
            renderer.Add(new Row().Add("nota", "a|b"));
            renderer.Finish(new EnvelopeInfo("detect"));
        });

        Assert.Contains("a\\|b", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_resultado_vacio_no_es_un_error_en_ningun_formato()
    {
        foreach (var format in new[] { OutputFormat.Table, OutputFormat.Md, OutputFormat.Csv, OutputFormat.Jsonl })
        {
            var output = Capture(() => new Renderer(Context(format)).Finish(new EnvelopeInfo("batch")));
            Assert.NotNull(output);
        }
    }

    [Fact]
    public void El_texto_acentuado_sobrevive_json_y_no_sale_escapado()
    {
        // Contrato sección 9: estas fuentes devuelven acentos y eñes constantemente.
        var output = Capture(() =>
        {
            var renderer = new Renderer(Context(OutputFormat.Json));
            renderer.Add(new Row().Add("nota", "análisis en español, Ñuñoa"));
            renderer.Finish(new EnvelopeInfo("detect"));
        });

        Assert.Contains("análisis en español, Ñuñoa", output, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(output);
        Assert.Equal("análisis en español, Ñuñoa", document.RootElement.GetProperty("rows")[0].GetProperty("nota").GetString());
    }

    [Fact]
    public void Con_output_los_datos_van_al_archivo_y_no_a_stdout()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".csv");
        try
        {
            var output = Capture(() =>
            {
                var renderer = new Renderer(Context(OutputFormat.Csv, new FileInfo(path)));
                renderer.Add(SampleRow());
                renderer.Finish(new EnvelopeInfo("detect"));
            });

            Assert.Equal(string.Empty, output.Trim());
            Assert.Contains("path,verdict", File.ReadAllText(path), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // --- filas irregulares --------------------------------------------------

    [Fact]
    public void La_cabecera_es_la_union_de_las_claves_de_todas_las_filas()
    {
        // En un lote con --verbose, un archivo que falló no trae columnas de señales. Si la
        // cabecera saliera sólo de la primera fila, el CSV perdería columnas según el orden.
        var rows = new[]
        {
            new Row().Add("path", "ok.txt").Add("verdict", "human"),
            new Row().Add("path", "malo.txt").Add("error_code", "api"),
        };

        var header = Row.HeaderOf(rows);
        Assert.Equal(["path", "verdict", "error_code"], header);
    }

    [Fact]
    public void Una_columna_ausente_sale_vacia_en_vez_de_desalinear()
    {
        var row = new Row().Add("path", "a.txt");
        Assert.Equal(string.Empty, row.Get("verdict").Display);
    }

    // --- errores ------------------------------------------------------------

    [Theory]
    [InlineData(ErrorCodes.Usage, 2)]
    [InlineData(ErrorCodes.NotFound, 3)]
    [InlineData(ErrorCodes.Auth, 4)]
    [InlineData(ErrorCodes.RateLimit, 5)]
    [InlineData(ErrorCodes.Network, 6)]
    [InlineData(ErrorCodes.Api, 1)]
    [InlineData(ErrorCodes.Internal, 1)]
    public void Cada_codigo_de_error_mapea_a_su_codigo_de_salida(string code, int expected)
    {
        Assert.Equal(expected, new CliError(code, "x", "y").ExitCode);
    }

    [Fact]
    public void El_sobre_de_error_va_por_stdout_en_formato_maquina()
    {
        var error = new CliError(ErrorCodes.NotFound, "no está", "revisa la ruta");
        var exitCode = 0;
        var output = Capture(() =>
            exitCode = Renderer.WriteError(Context(OutputFormat.Json), "detect", error));

        Assert.Equal(3, exitCode);
        using var document = JsonDocument.Parse(output);
        Assert.False(document.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("iadetector", document.RootElement.GetProperty("source").GetString());
        Assert.Equal("not_found", document.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("revisa la ruta", document.RootElement.GetProperty("error").GetProperty("hint").GetString());
    }

    [Fact]
    public void En_modo_table_el_error_no_ensucia_stdout()
    {
        var error = new CliError(ErrorCodes.Usage, "mal invocado", "usa --help");
        var stdout = string.Empty;
        var stderr = CaptureError(() =>
            stdout = Capture(() => Renderer.WriteError(Context(OutputFormat.Table), "detect", error)));

        Assert.Equal(string.Empty, stdout.Trim());
        Assert.Contains("mal invocado", stderr, StringComparison.Ordinal);
        Assert.Contains("usa --help", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void La_tabla_de_codigos_de_salida_cubre_los_siete_del_contrato()
    {
        Assert.Equal([0, 1, 2, 3, 4, 5, 6], ExitCodes.Table.Select(e => e.Code).Order());
    }

    [Fact]
    public void Los_codigos_que_no_aplican_se_documentan_en_vez_de_inventarse()
    {
        // Este proyecto no tiene API externa: fingir un significado para auth o rate_limit
        // sería peor que decir que no aplican.
        foreach (var code in new[] { ExitCodes.Auth, ExitCodes.RateLimit })
        {
            var meaning = ExitCodes.Table.Single(e => e.Code == code).Meaning;
            Assert.Contains("No aplica", meaning, StringComparison.Ordinal);
        }
    }
}
