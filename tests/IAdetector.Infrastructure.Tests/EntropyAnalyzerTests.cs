using IAdetector.Core.Domain;
using IAdetector.Infrastructure.Signals;
using Xunit;

namespace IAdetector.Infrastructure.Tests;

public class EntropyAnalyzerTests
{
    private readonly EntropyAnalyzer _sut = new();

    [Fact]
    public async Task AnalyzeAsync_FewerThanFourHundredWords_ReturnsNeutralWithReducedWeight()
    {
        var sample = TextSample.Create(string.Join(' ', Enumerable.Repeat("word", 100)));

        var score = await _sut.AnalyzeAsync(sample);

        Assert.Equal(0.5, score.NormalizedScore);
        Assert.True(score.Weight < 0.8);
    }

    [Fact]
    public async Task AnalyzeAsync_SkewedVocabulary_ScoresHigherThanDiverseFixtureText()
    {
        // Distribución muy sesgada (una palabra dominante + 20 raras) => entropía relativa baja,
        // a diferencia de una distribución uniforme sobre pocas palabras, que en realidad
        // maximizaría la entropía relativa.
        var fillers = new[]
        {
            "red", "blue", "green", "yellow", "purple", "orange", "pink", "cyan", "black", "white",
            "brown", "gray", "gold", "silver", "teal", "navy", "coral", "olive", "maroon", "indigo",
        };
        var skewedWords = Enumerable.Repeat("word", 480).Concat(fillers);
        var skewed = TextSample.Create(string.Join(' ', skewedWords));

        var diverseText = await File.ReadAllTextAsync(FixturePaths.Human("long_rant1.txt"));
        var diverse = TextSample.Create(diverseText);

        var skewedScore = await _sut.AnalyzeAsync(skewed);
        var diverseScore = await _sut.AnalyzeAsync(diverse);

        Assert.True(skewedScore.NormalizedScore > diverseScore.NormalizedScore);
    }
}
