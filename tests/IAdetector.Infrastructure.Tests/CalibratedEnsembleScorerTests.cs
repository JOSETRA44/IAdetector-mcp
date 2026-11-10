using IAdetector.Core.Domain;
using IAdetector.Infrastructure.Ensemble;
using Xunit;

namespace IAdetector.Infrastructure.Tests;

public class CalibratedEnsembleScorerTests
{
    private readonly TextSample _sample = TextSample.Create("irrelevant for these tests");

    // Pesos sintéticos (no los reales de producción) que producen sigmoid(z) predecible:
    // z = -5 + 10 * NormalizedScore("a").
    private static readonly EnsembleWeightsData TestWeights = new(Intercept: -5.0, Weights: new() { ["a"] = 10.0 });

    [Fact]
    public void Combine_StrongPositiveSignal_YieldsAiVerdict()
    {
        var sut = new CalibratedEnsembleScorer(TestWeights);
        var scores = new[] { new SignalScore("a", 1.0, 1.0, 1.0, "x") };

        var result = sut.Combine(_sample, scores);

        Assert.Equal(Verdict.AI, result.Verdict);
        Assert.True(result.AiProbability > 0.99);
    }

    [Fact]
    public void Combine_StrongNegativeSignal_YieldsHumanVerdict()
    {
        var sut = new CalibratedEnsembleScorer(TestWeights);
        var scores = new[] { new SignalScore("a", 0.0, 0.0, 1.0, "x") };

        var result = sut.Combine(_sample, scores);

        Assert.Equal(Verdict.Human, result.Verdict);
        Assert.True(result.AiProbability < 0.01);
    }

    [Fact]
    public void Combine_NeutralSignal_YieldsMixedVerdict()
    {
        var sut = new CalibratedEnsembleScorer(TestWeights);
        var scores = new[] { new SignalScore("a", 0.5, 0.5, 1.0, "x") };

        var result = sut.Combine(_sample, scores);

        Assert.Equal(Verdict.Mixed, result.Verdict);
        Assert.Equal(0.5, result.AiProbability, precision: 6);
    }

    [Fact]
    public void Combine_UnknownSignalName_IsIgnoredByCalibratedWeights()
    {
        var sut = new CalibratedEnsembleScorer(TestWeights);
        // "b" no existe en TestWeights: no debería influir en z (solo el intercept cuenta).
        var scores = new[] { new SignalScore("b", 1.0, 1.0, 1.0, "x") };

        var result = sut.Combine(_sample, scores);

        Assert.Equal(Sigmoid(-5.0), result.AiProbability, precision: 6);
    }

    [Fact]
    public void Combine_AgreeingSignals_YieldsHigherConfidenceThanDisagreeingSignals()
    {
        var sut = new CalibratedEnsembleScorer(TestWeights);
        var agreeing = new[]
        {
            new SignalScore("a", 0.8, 0.8, 1.0, "x"),
            new SignalScore("a", 0.82, 0.82, 1.0, "y"),
        };
        var disagreeing = new[]
        {
            new SignalScore("a", 0.0, 0.0, 1.0, "x"),
            new SignalScore("a", 1.0, 1.0, 1.0, "y"),
        };

        var agreeingResult = sut.Combine(_sample, agreeing);
        var disagreeingResult = sut.Combine(_sample, disagreeing);

        Assert.True(agreeingResult.Confidence > disagreeingResult.Confidence);
    }

    [Fact]
    public void Combine_AllSignalsWithoutData_ReturnsNeutralMixedWithZeroConfidence()
    {
        // Regresión: con los modelos ONNX sin descargar o un texto demasiado corto, todas
        // las señales devuelven el 0.5 convencional de «sin datos». Antes el ensemble lo
        // convertía en «probablemente humano» con confianza ≈0.78; sin evidencia no debe
        // haber veredicto, igual que cuando no llega ninguna señal.
        var sut = new CalibratedEnsembleScorer();
        var scores = new[]
        {
            new SignalScore("burstiness", 0.0, 0.5, 0.2, "sin datos", HasData: false),
            new SignalScore("onnx-classifier", 0.0, 0.5, 0.4, "modelo no descargado", HasData: false),
            new SignalScore("perplexity", 0.0, 0.5, 0.4, "modelo no descargado", HasData: false),
        };

        var result = sut.Combine(_sample, scores);

        Assert.Equal(Verdict.Mixed, result.Verdict);
        Assert.Equal(0.5, result.AiProbability);
        Assert.Equal(0.0, result.Confidence);
    }

    [Fact]
    public void Combine_OneSignalWithDataAmongUnavailable_StillProducesVerdict()
    {
        var sut = new CalibratedEnsembleScorer(TestWeights);
        var scores = new[]
        {
            new SignalScore("a", 1.0, 1.0, 1.0, "medido"),
            new SignalScore("a", 0.0, 0.5, 0.2, "sin datos", HasData: false),
        };

        var result = sut.Combine(_sample, scores);

        Assert.Equal(Verdict.AI, result.Verdict);
    }

    [Fact]
    public void Combine_AgreeingSignalsAtExtreme_YieldFullConfidence()
    {
        // Regresión: la dispersión se mide contra la media de las señales, no contra la
        // probabilidad sigmoide. Dos señales que coinciden exactamente tienen dispersión
        // cero y confianza plena, aunque la probabilidad final sea extrema.
        var sut = new CalibratedEnsembleScorer(TestWeights);
        var scores = new[]
        {
            new SignalScore("a", 0.85, 0.85, 1.0, "x"),
            new SignalScore("a", 0.85, 0.85, 1.0, "y"),
        };

        var result = sut.Combine(_sample, scores);

        Assert.Equal(1.0, result.Confidence, precision: 6);
    }

    [Fact]
    public void Combine_NoScores_ReturnsNeutralMixedWithZeroConfidence()
    {
        var sut = new CalibratedEnsembleScorer(TestWeights);

        var result = sut.Combine(_sample, []);

        Assert.Equal(Verdict.Mixed, result.Verdict);
        Assert.Equal(0.5, result.AiProbability);
        Assert.Equal(0.0, result.Confidence);
    }

    [Fact]
    public void Combine_PassesChunkInfoThrough()
    {
        var sut = new CalibratedEnsembleScorer(TestWeights);
        var chunkInfo = new ChunkInfo(3, 5000);
        var scores = new[] { new SignalScore("a", 0.5, 0.5, 1.0, "x") };

        var result = sut.Combine(_sample, scores, chunkInfo);

        Assert.Same(chunkInfo, result.ChunkInfo);
    }

    [Fact]
    public void ParameterlessConstructor_LoadsEmbeddedProductionWeights()
    {
        // Guarda contra un nombre de recurso embebido incorrecto o un JSON corrupto en el paquete.
        var sut = new CalibratedEnsembleScorer();
        var scores = new[] { new SignalScore("onnx-classifier", 0.9, 0.9, 2.0, "x") };

        var result = sut.Combine(_sample, scores);

        Assert.InRange(result.AiProbability, 0.0, 1.0);
    }

    private static double Sigmoid(double z) => 1.0 / (1.0 + Math.Exp(-z));
}

public class CalibratedEnsembleScorerLanguageRoutingTests
{
    private const string SpanishText =
        "El zorro marrón salta sobre el perro mientras el granjero observa el campo, y la "
        + "gente del pueblo comenta que es una escena de lo más curiosa para un día de mercado.";

    private const string EnglishText =
        "The quick brown fox jumps over the lazy dog while the farmer watches the field, "
        + "and the people of the village say that it is a curious scene for a market day.";

    private static readonly EnsembleWeightsData EnglishWeights = new(
        Intercept: -20.0, Weights: new Dictionary<string, double> { ["burstiness"] = 0.0 });

    private static readonly EnsembleWeightsData SpanishWeights = new(
        Intercept: 20.0, Weights: new Dictionary<string, double> { ["burstiness"] = 0.0 });

    [Fact]
    public void Combine_SpanishSample_UsesSpanishProfile()
    {
        var sut = new CalibratedEnsembleScorer(EnglishWeights, SpanishWeights);
        var scores = new[] { new SignalScore("burstiness", 0.5, 0.5, 1.0, "") };

        var result = sut.Combine(TextSample.Create(SpanishText), scores);

        Assert.True(result.AiProbability > 0.99);
    }

    [Fact]
    public void Combine_EnglishSample_UsesEnglishProfile()
    {
        var sut = new CalibratedEnsembleScorer(EnglishWeights, SpanishWeights);
        var scores = new[] { new SignalScore("burstiness", 0.5, 0.5, 1.0, "") };

        var result = sut.Combine(TextSample.Create(EnglishText), scores);

        Assert.True(result.AiProbability < 0.01);
    }
}
