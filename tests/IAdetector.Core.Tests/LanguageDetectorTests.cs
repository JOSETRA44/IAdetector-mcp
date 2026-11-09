using IAdetector.Core.Domain;
using IAdetector.Core.Tests.Fakes;
using IAdetector.Core.UseCases;
using Xunit;

namespace IAdetector.Core.Tests;

public class LanguageDetectorTests
{
    private const string SpanishText =
        "El zorro marrón salta sobre el perro mientras el granjero observa el campo, y la "
        + "gente del pueblo comenta que es una escena de lo más curiosa para un día de mercado.";

    private const string EnglishText =
        "The quick brown fox jumps over the lazy dog while the farmer watches the field, "
        + "and the people of the village say that it is a curious scene for a market day.";

    [Fact]
    public void Detect_SpanishProse_Spanish() =>
        Assert.Equal(TextLanguage.Spanish, LanguageDetector.Detect(TextSample.Create(SpanishText)));

    [Fact]
    public void Detect_EnglishProse_English() =>
        Assert.Equal(TextLanguage.English, LanguageDetector.Detect(TextSample.Create(EnglishText)));

    [Fact]
    public void Detect_TooFewFunctionWords_Unknown() =>
        Assert.Equal(TextLanguage.Unknown, LanguageDetector.Detect(TextSample.Create("Hola mundo.")));

    [Fact]
    public void Detect_MixedWithoutClearMajority_Unknown()
    {
        var mixed = "el the la of los and las in un is una que that por it con";
        Assert.Equal(TextLanguage.Unknown, LanguageDetector.Detect(TextSample.Create(mixed)));
    }

    [Fact]
    public async Task DetectTextUseCase_SpanishText_ResultCarriesSpanish()
    {
        var analyzer = new RecordingAnalyzer("fake", _ => new SignalScore("fake", 0.3, 0.3, 1.0, ""));
        var useCase = new DetectTextUseCase([analyzer], new RecordingScorer());

        var result = await useCase.DetectAsync(SpanishText);

        Assert.Equal(TextLanguage.Spanish, result.DetectedLanguage);
    }
}
