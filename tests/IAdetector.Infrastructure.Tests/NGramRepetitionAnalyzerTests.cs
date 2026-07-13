using IAdetector.Core.Domain;
using IAdetector.Infrastructure.Signals;
using Xunit;

namespace IAdetector.Infrastructure.Tests;

public class NGramRepetitionAnalyzerTests
{
    private readonly NGramRepetitionAnalyzer _sut = new();

    [Fact]
    public async Task AnalyzeAsync_TooFewWords_ReturnsNeutralWithReducedWeight()
    {
        var sample = TextSample.Create("Just a handful of words here, not enough for trigrams.");

        var score = await _sut.AnalyzeAsync(sample);

        Assert.Equal(0.5, score.NormalizedScore);
        Assert.True(score.Weight < 0.8);
    }

    [Fact]
    public async Task AnalyzeAsync_RepeatedPhrases_ScoresHigherThanUniqueText()
    {
        var repeated = TextSample.Create(string.Concat(Enumerable.Repeat("the quick brown fox jumps over the lazy dog and runs away fast. ", 20)));
        var uniqueText = await File.ReadAllTextAsync(FixturePaths.Human("long_rant1.txt"));
        var unique = TextSample.Create(uniqueText);

        var repeatedScore = await _sut.AnalyzeAsync(repeated);
        var uniqueScore = await _sut.AnalyzeAsync(unique);

        Assert.True(repeatedScore.NormalizedScore > uniqueScore.NormalizedScore);
        Assert.True(repeatedScore.RawValue > 0.5);
    }
}
