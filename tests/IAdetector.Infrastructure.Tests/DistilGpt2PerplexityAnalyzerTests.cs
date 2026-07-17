using IAdetector.Core.Domain;
using IAdetector.Infrastructure.Ml;
using Xunit;

namespace IAdetector.Infrastructure.Tests;

/// <summary>Requiere el modelo descargado (<c>iadetector models download --model distilgpt2-perplexity</c>); se omite si no está presente.</summary>
public class DistilGpt2PerplexityAnalyzerTests
{
    [Fact]
    public async Task AnalyzeAsync_TooFewWords_ReturnsNeutralWithReducedWeight()
    {
        var cache = new FileSystemModelCache();
        var sut = new DistilGpt2PerplexityAnalyzer(cache);
        var sample = TextSample.Create("Just a handful of words here.");

        var score = await sut.AnalyzeAsync(sample);

        Assert.Equal(0.5, score.NormalizedScore);
        Assert.True(score.Weight < 1.0);
    }

    [Fact]
    public async Task AnalyzeAsync_RealGpt2Output_ScoresLowerPerplexityThanHumanFixture()
    {
        var cache = new FileSystemModelCache();
        if (!cache.IsDownloaded(ModelCatalog.DistilGpt2Perplexity))
        {
            return;
        }

        var sut = new DistilGpt2PerplexityAnalyzer(cache);

        var gpt2Text = await File.ReadAllTextAsync(FixturePaths.Ai("gpt2_unicorns.txt"));
        var humanText = await File.ReadAllTextAsync(FixturePaths.Human("long_rant1.txt"));

        var gpt2Score = await sut.AnalyzeAsync(TextSample.Create(gpt2Text));
        var humanScore = await sut.AnalyzeAsync(TextSample.Create(humanText));

        Assert.True(gpt2Score.RawValue < humanScore.RawValue, "Se esperaba menor perplexity (más predecible) para la salida real de GPT-2 que para el texto humano.");
        Assert.True(gpt2Score.NormalizedScore > humanScore.NormalizedScore);
    }
}
