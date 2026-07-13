using IAdetector.Core.Domain;
using Xunit;

namespace IAdetector.Core.Tests;

public class TextTokenizerTests
{
    [Fact]
    public void SplitSentences_EmptyText_ReturnsEmpty()
    {
        Assert.Empty(TextTokenizer.SplitSentences(""));
        Assert.Empty(TextTokenizer.SplitSentences("   "));
    }

    [Fact]
    public void SplitSentences_SplitsOnTerminalPunctuationFollowedByCapital()
    {
        var sentences = TextTokenizer.SplitSentences("This is one. This is two! Is this three? Yes.");

        Assert.Equal(4, sentences.Count);
        Assert.Equal("This is one.", sentences[0]);
        Assert.Equal("Yes.", sentences[3]);
    }

    [Fact]
    public void SplitSentences_DoesNotSplitOnAbbreviationFollowedByLowercase()
    {
        var sentences = TextTokenizer.SplitSentences("I met Mr. smith yesterday at the park.");

        Assert.Single(sentences);
    }

    [Fact]
    public void SplitWords_CountsWordsIgnoringPunctuation()
    {
        var words = TextTokenizer.SplitWords("Hello, world! It's a test-case.");

        Assert.Equal(["Hello", "world", "It's", "a", "test-case"], words);
    }

    [Fact]
    public void SplitWords_EmptyText_ReturnsEmpty()
    {
        Assert.Empty(TextTokenizer.SplitWords(""));
    }
}
