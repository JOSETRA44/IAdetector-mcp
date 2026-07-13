using IAdetector.Core.Domain;
using IAdetector.Infrastructure.Signals;
using Xunit;

namespace IAdetector.Infrastructure.Tests;

public class CompressionRatioAnalyzerTests
{
    private readonly CompressionRatioAnalyzer _sut = new();

    [Fact]
    public async Task AnalyzeAsync_ShortText_ReturnsNeutralWithReducedWeight()
    {
        var sample = TextSample.Create("A short sentence.");

        var score = await _sut.AnalyzeAsync(sample);

        Assert.Equal(0.5, score.NormalizedScore);
        Assert.True(score.Weight < 1.2);
    }

    [Fact]
    public async Task AnalyzeAsync_RepetitiveText_ScoresHigherThanVariedText()
    {
        var repetitive = TextSample.Create(string.Concat(Enumerable.Repeat("the quick brown fox jumps over the lazy dog. ", 60)));
        var variedText = await File.ReadAllTextAsync(FixturePaths.Human("long_rant1.txt"));
        var varied = TextSample.Create(variedText);

        var repetitiveScore = await _sut.AnalyzeAsync(repetitive);
        var variedScore = await _sut.AnalyzeAsync(varied);

        Assert.True(repetitiveScore.NormalizedScore > variedScore.NormalizedScore);
    }
}
