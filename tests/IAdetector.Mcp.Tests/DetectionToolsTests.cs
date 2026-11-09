using System.Text.Json;
using IAdetector.Core.Abstractions;
using IAdetector.Core.UseCases;
using IAdetector.Infrastructure.DependencyInjection;
using IAdetector.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using Xunit;

namespace IAdetector.Mcp.Tests;

/// <summary>
/// Pruebas de contrato de las tools MCP: se construyen con la DI real
/// (AddIAdetectorInfrastructure) y textos muy cortos, donde la mayoría de señales no
/// tiene datos — justo el caso límite que la Fase 1.1 convirtió en «sin evidencia» y
/// que explain_detection y compare_versions deben reflejar con honestidad.
/// </summary>
public class DetectionToolsTests
{
    private static DetectionTools BuildTools()
    {
        var provider = new ServiceCollection()
            .AddIAdetectorInfrastructure()
            .BuildServiceProvider();
        return new DetectionTools(
            provider.GetRequiredService<DetectTextUseCase>(),
            provider.GetRequiredService<BatchDetectUseCase>(),
            provider.GetRequiredService<IDetectionExplainer>(),
            provider.GetRequiredService<CompareVersionsUseCase>());
    }

    private const string TinyText = "Hola mundo, esto es una prueba.";

    [Fact]
    public async Task ExplainDetection_TinyText_ReturnsDiagnosticsWithNoDataStatuses()
    {
        var json = await BuildTools().ExplainDetection(TinyText);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.GetProperty("signals").GetArrayLength() > 0);
        Assert.True(root.TryGetProperty("prioritizedActions", out _));
        // Un texto de 6 palabras no da evidencia: ninguna señal estadística mide y los
        // modelos ONNX exigen más tokens, así que no puede haber acciones priorizadas.
        Assert.Equal(0, root.GetProperty("prioritizedActions").GetArrayLength());
        Assert.Contains(root.GetProperty("signals").EnumerateArray(),
            s => s.GetProperty("status").GetString() == "sin_datos");
    }

    [Fact]
    public async Task CompareVersions_IdenticalTinyTexts_InconclusiveForLackOfEvidence()
    {
        var json = await BuildTools().CompareVersions(TinyText, TinyText);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("no_concluyente", root.GetProperty("outcome").GetString());
        Assert.Equal(1.0, root.GetProperty("semanticSimilarity").GetDouble());
        Assert.Equal(0.0, root.GetProperty("deltaProbability").GetDouble());
    }

    [Fact]
    public async Task ExplainDetection_SpanishText_CarriesLanguageAndWarning()
    {
        const string spanish =
            "El zorro marrón salta sobre el perro mientras el granjero observa el campo, y la "
            + "gente del pueblo comenta que es una escena de lo más curiosa para un día de mercado.";

        var json = await BuildTools().ExplainDetection(spanish);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Spanish", doc.RootElement.GetProperty("language").GetString());
        Assert.True(doc.RootElement.GetProperty("warnings").GetArrayLength() > 0);
    }

    [Fact]
    public async Task CompareVersions_EmptyText_Throws()
    {
        await Assert.ThrowsAsync<McpException>(() => BuildTools().CompareVersions("", TinyText));
        await Assert.ThrowsAsync<McpException>(() => BuildTools().CompareVersions(TinyText, " "));
    }

    [Fact]
    public async Task DetectText_TinyText_StillReturnsVerdictContract()
    {
        var json = await BuildTools().DetectText(TinyText);

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("verdict", out _));
        Assert.True(doc.RootElement.TryGetProperty("aiProbability", out _));
    }
}
