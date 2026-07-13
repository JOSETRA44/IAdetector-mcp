using IAdetector.Core.Domain;
using IAdetector.Infrastructure.Signals;
using Xunit;

namespace IAdetector.Infrastructure.Tests;

public class BurstinessAnalyzerTests
{
    private readonly BurstinessAnalyzer _sut = new();

    [Fact]
    public async Task AnalyzeAsync_TooFewSentences_ReturnsNeutralWithReducedWeight()
    {
        var sample = TextSample.Create("One. Two.");

        var score = await _sut.AnalyzeAsync(sample);

        Assert.Equal(0.5, score.NormalizedScore);
        Assert.True(score.Weight < 1.0);
    }

    [Fact]
    public async Task AnalyzeAsync_UniformSentenceLengths_ScoresHigherThanVariedLengths()
    {
        var uniform = TextSample.Create(string.Concat(Enumerable.Repeat("word word word word word word. ", 10)));
        var varied = TextSample.Create(
            "Hi. " +
            "This one has a moderate number of words in it. " +
            "Short. " +
            "This sentence, on the other hand, goes on for quite a while with many extra words attached to it. " +
            "Ok then. " +
            "Another long one that rambles for a good while just to add some real variance here.");

        var uniformScore = await _sut.AnalyzeAsync(uniform);
        var variedScore = await _sut.AnalyzeAsync(varied);

        Assert.True(uniformScore.NormalizedScore > variedScore.NormalizedScore);
    }
}
