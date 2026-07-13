using IAdetector.Core.Domain;
using IAdetector.Infrastructure.Ensemble;
using Xunit;

namespace IAdetector.Infrastructure.Tests;

public class WeightedEnsembleScorerTests
{
    private readonly WeightedEnsembleScorer _sut = new();
    private readonly TextSample _sample = TextSample.Create("irrelevant for these tests");

    [Theory]
    [InlineData(0.05, Verdict.Human)]
    [InlineData(0.3, Verdict.LikelyHuman)]
    [InlineData(0.5, Verdict.Mixed)]
    [InlineData(0.7, Verdict.LikelyAI)]
    [InlineData(0.95, Verdict.AI)]
    public void Combine_MapsAiProbabilityToExpectedVerdict(double normalizedScore, Verdict expected)
    {
        var scores = new[] { new SignalScore("only", normalizedScore, normalizedScore, 1.0, "x") };

        var result = _sut.Combine(_sample, scores);

        Assert.Equal(expected, result.Verdict);
        Assert.Equal(normalizedScore, result.AiProbability, precision: 6);
    }

    [Fact]
    public void Combine_WeightsSignalsProportionallyToWeight()
    {
        var scores = new[]
        {
            new SignalScore("low-weight", 1.0, 1.0, 0.1, "x"),
            new SignalScore("high-weight", 0.0, 0.0, 10.0, "y"),
        };

        var result = _sut.Combine(_sample, scores);

        Assert.True(result.AiProbability < 0.2);
    }

    [Fact]
    public void Combine_AgreeingSignals_YieldsHigherConfidenceThanDisagreeingSignals()
    {
        var agreeing = new[]
        {
            new SignalScore("a", 0.8, 0.8, 1.0, "x"),
            new SignalScore("b", 0.82, 0.82, 1.0, "y"),
        };
        var disagreeing = new[]
        {
            new SignalScore("a", 0.0, 0.0, 1.0, "x"),
            new SignalScore("b", 1.0, 1.0, 1.0, "y"),
        };

        var agreeingResult = _sut.Combine(_sample, agreeing);
        var disagreeingResult = _sut.Combine(_sample, disagreeing);

        Assert.True(agreeingResult.Confidence > disagreeingResult.Confidence);
    }

    [Fact]
    public void Combine_NoScores_ReturnsNeutralMixedWithZeroConfidence()
    {
        var result = _sut.Combine(_sample, []);

        Assert.Equal(Verdict.Mixed, result.Verdict);
        Assert.Equal(0.5, result.AiProbability);
        Assert.Equal(0.0, result.Confidence);
    }

    [Fact]
    public void Combine_PassesChunkInfoThrough()
    {
        var chunkInfo = new ChunkInfo(3, 5000);
        var scores = new[] { new SignalScore("a", 0.5, 0.5, 1.0, "x") };

        var result = _sut.Combine(_sample, scores, chunkInfo);

        Assert.Same(chunkInfo, result.ChunkInfo);
    }
}
