using IAdetector.Core.Abstractions;
using IAdetector.Core.Domain;
using IAdetector.Core.Tests.Fakes;
using IAdetector.Core.UseCases;
using Xunit;

namespace IAdetector.Core.Tests;

/// <summary>
/// Pruebas del bucle de reescritura: el use case con analizadores falsos deterministas y
/// un explicador falso con coeficiente 1 y rango humano [0, 1] (todo «en rango»), para
/// aislar la lógica de comparación y de salvaguardas de la calibración real.
/// </summary>
public class CompareVersionsUseCaseTests
{
    private sealed class PassThroughExplainer : IDetectionExplainer
    {
        public DetectionExplanation Explain(DetectionResult result) => new(
            result.Verdict,
            result.AiProbability,
            result.Confidence,
            result.Signals.Count(s => s.HasData),
            result.Signals.Count,
            result.Signals.Select(s => new SignalDiagnostic(
                s.Name, s.HasData, s.RawValue, s.NormalizedScore,
                Coefficient: 1.0, LogitContribution: s.NormalizedScore,
                new HumanRange(0.0, 0.5, 1.0, 100), SignalStatus.InHumanRange, string.Empty)).ToList(),
            []);
    }

    private static CompareVersionsUseCase BuildUseCase(Func<TextSample, SignalScore> score) =>
        new(
            new DetectTextUseCase([new RecordingAnalyzer("fake", score)], new RecordingScorer()),
            new PassThroughExplainer());

    private static readonly string BaseText =
        "el zorro marrón rápido salta sobre el perro perezoso mientras el granjero observa el campo tranquilo";

    [Fact]
    public async Task CompareAsync_IdenticalTexts_UnchangedAndFullSimilarity()
    {
        var useCase = BuildUseCase(_ => new SignalScore("fake", 0.7, 0.7, 1.0, ""));

        var comparison = await useCase.CompareAsync(BaseText, BaseText);

        Assert.Equal(ComparisonOutcome.Unchanged, comparison.Outcome);
        Assert.Equal(0.0, comparison.DeltaProbability);
        Assert.Equal(1.0, comparison.SemanticSimilarity);
        Assert.Empty(comparison.Warnings);
    }

    [Fact]
    public async Task CompareAsync_LowerScoreKeepingVocabulary_Improved()
    {
        var useCase = BuildUseCase(sample => sample.Text.Contains("uniforme")
            ? new SignalScore("fake", 0.9, 0.9, 1.0, "")
            : new SignalScore("fake", 0.1, 0.1, 1.0, ""));

        var comparison = await useCase.CompareAsync(
            "texto uniforme " + BaseText, BaseText);

        Assert.Equal(ComparisonOutcome.Improved, comparison.Outcome);
        Assert.True(comparison.DeltaProbability < 0);
        Assert.True(comparison.SemanticSimilarity >= CompareVersionsUseCase.MinSemanticSimilarity);
        var delta = Assert.Single(comparison.SignalDeltas);
        Assert.Equal(-0.8, delta.Delta, precision: 10);
        Assert.Equal(-0.8, delta.LogitDelta!.Value, precision: 10);
    }

    [Fact]
    public async Task CompareAsync_RewriteLosingVocabulary_InconclusiveEvenIfScoreDrops()
    {
        var useCase = BuildUseCase(sample => sample.Text.Contains("uniforme")
            ? new SignalScore("fake", 0.9, 0.9, 1.0, "")
            : new SignalScore("fake", 0.1, 0.1, 1.0, ""));

        var comparison = await useCase.CompareAsync(
            "texto uniforme " + BaseText,
            "quantum zebra xilófono yogurt kiwi wagner victoria whisky xenón zumo");

        Assert.Equal(ComparisonOutcome.Inconclusive, comparison.Outcome);
        Assert.True(comparison.SemanticSimilarity < CompareVersionsUseCase.MinSemanticSimilarity);
        Assert.Contains(comparison.Warnings, w => w.Contains("vocabulario de contenido"));
    }

    [Fact]
    public async Task CompareAsync_NoSignalData_InconclusiveWithWarning()
    {
        var useCase = BuildUseCase(_ => new SignalScore("fake", 0.5, 0.5, 1.0, "", HasData: false));

        var comparison = await useCase.CompareAsync(BaseText, BaseText);

        Assert.Equal(ComparisonOutcome.Inconclusive, comparison.Outcome);
        Assert.Contains(comparison.Warnings, w => w.Contains("Evidencia insuficiente"));
    }

    [Theory]
    [InlineData("perro gato casa árbol", "perro gato casa árbol", 1.0)]
    [InlineData("perro gato casa árbol", "quantum zebra xilófono yogurt", 0.0)]
    [InlineData("sol la el", "mar en un", 1.0)] // sin palabras de contenido en ninguno
    public void ContentWordJaccard_KnownCases(string first, string second, double expected)
    {
        Assert.Equal(expected, CompareVersionsUseCase.ContentWordJaccard(first, second), precision: 10);
    }
}
