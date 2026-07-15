using IAdetector.Infrastructure.Ml;
using Microsoft.ML.Tokenizers;
using Xunit;

namespace IAdetector.Infrastructure.Tests;

/// <summary>
/// Requiere el modelo descargado (<c>iadetector models download</c>); se omite si no está
/// presente, ya que no se distribuye con el repo. Cubre una regresión real encontrada en
/// desarrollo: sin <see cref="BpeOptions.ByteLevel"/>=true, el tokenizador no antepone el
/// marcador de espacio byte-level ('Ġ') y el clasificador queda esencialmente ciego al contenido
/// (todo texto de longitud normal puntuaba ~0% de IA, incluyendo salidas reales de GPT-2).
/// </summary>
public class RobertaOnnxClassifierTests
{
    [Fact]
    public void Tokenizer_AddsByteLevelSpacePrefixMarker()
    {
        if (!TryGetModelDir(out var dir))
        {
            return;
        }

        var tokenizer = CreateTokenizer(dir);

        // vocab.json real: "Hello"=31414, "Ġworld"=232 (NO "world"=8331, que sería el bug).
        var ids = tokenizer.EncodeToIds("Hello world");

        Assert.Equal([31414, 232], ids);
    }

    [Fact]
    public async Task ClassifyAsync_RealGpt2Output_ScoresHighAiProbability()
    {
        if (!TryGetModelDir(out _))
        {
            return;
        }

        var classifier = new RobertaOnnxClassifier(new FileSystemModelCache());
        var text = await File.ReadAllTextAsync(FixturePaths.Ai("gpt2_unicorns.txt"));

        var probability = await classifier.ClassifyAsync(text);

        Assert.True(probability > 0.9, $"Se esperaba alta probabilidad de IA para una salida real de GPT-2, se obtuvo {probability:F4}.");
    }

    [Fact]
    public async Task ClassifyAsync_HumanFixture_ScoresLowAiProbability()
    {
        if (!TryGetModelDir(out _))
        {
            return;
        }

        var classifier = new RobertaOnnxClassifier(new FileSystemModelCache());
        var text = await File.ReadAllTextAsync(FixturePaths.Human("long_rant1.txt"));

        var probability = await classifier.ClassifyAsync(text);

        Assert.True(probability < 0.1, $"Se esperaba baja probabilidad de IA para texto humano, se obtuvo {probability:F4}.");
    }

    private static bool TryGetModelDir(out string dir)
    {
        var cache = new FileSystemModelCache();
        if (!cache.IsDownloaded(ModelCatalog.RobertaOpenAiDetector))
        {
            dir = "";
            return false;
        }

        dir = cache.GetPath(ModelCatalog.RobertaOpenAiDetector);
        return true;
    }

    private static BpeTokenizer CreateTokenizer(string dir)
    {
        var options = new BpeOptions(Path.Combine(dir, "vocab.json"), Path.Combine(dir, "merges.txt"))
        {
            PreTokenizer = RobertaPreTokenizer.Instance,
            ByteLevel = true,
        };
        return BpeTokenizer.Create(options);
    }
}
