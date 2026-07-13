using IAdetector.Core.Domain;
using Xunit;

namespace IAdetector.Core.Tests;

public class TextSampleTests
{
    [Fact]
    public void Create_PopulatesCountsFromTokenization()
    {
        var sample = TextSample.Create("One two three. Four five six seven.");

        Assert.Equal(2, sample.SentenceCount);
        Assert.Equal(7, sample.WordCount);
        Assert.Equal(sample.Text.Length, sample.CharacterCount);
    }

    [Fact]
    public void Create_TrimsSurroundingWhitespace()
    {
        var sample = TextSample.Create("   hello world   ");

        Assert.Equal("hello world", sample.Text);
    }

    [Fact]
    public void Create_EmptyText_HasZeroCounts()
    {
        var sample = TextSample.Create("");

        Assert.Equal(0, sample.WordCount);
        Assert.Equal(0, sample.SentenceCount);
    }
}
