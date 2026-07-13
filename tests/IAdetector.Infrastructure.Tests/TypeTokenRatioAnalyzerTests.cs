using IAdetector.Core.Domain;
using IAdetector.Infrastructure.Signals;
using Xunit;

namespace IAdetector.Infrastructure.Tests;

public class TypeTokenRatioAnalyzerTests
{
    private readonly TypeTokenRatioAnalyzer _sut = new();

    [Fact]
    public async Task AnalyzeAsync_FewerThanFourHundredWords_ReturnsNeutralWithReducedWeight()
    {
        var sample = TextSample.Create(string.Join(' ', Enumerable.Repeat("word", 100)));

        var score = await _sut.AnalyzeAsync(sample);

        Assert.Equal(0.5, score.NormalizedScore);
        Assert.True(score.Weight < 0.8);
    }

    [Fact]
    public async Task AnalyzeAsync_RepeatedSingleWord_ScoresHigherThanDiverseFixtureText()
    {
        var repetitive = TextSample.Create(string.Join(' ', Enumerable.Repeat("word", 500)));
        var diverseText = await File.ReadAllTextAsync(FixturePaths.Human("long_rant1.txt"));
        var diverse = TextSample.Create(diverseText);

        var repetitiveScore = await _sut.AnalyzeAsync(repetitive);
        var diverseScore = await _sut.AnalyzeAsync(diverse);

        Assert.True(repetitiveScore.NormalizedScore > diverseScore.NormalizedScore);
    }
}
