using IAdetector.Infrastructure.Ml;
using Xunit;

namespace IAdetector.Infrastructure.Tests;

public class TokenWindowsTests
{
    [Fact]
    public void Starts_TextFitsInWindow_SingleWindowAtZero()
    {
        Assert.Equal([0], TokenWindows.Starts(totalTokens: 200, windowSize: 256));
        Assert.Equal([0], TokenWindows.Starts(totalTokens: 256, windowSize: 256));
    }

    [Fact]
    public void Starts_LongText_CoversStartAndEndWithinMaxWindows()
    {
        var starts = TokenWindows.Starts(totalTokens: 2_600, windowSize: 510);

        Assert.True(starts.Count is >= 2 and <= TokenWindows.MaxWindows);
        Assert.Equal(0, starts[0]);
        Assert.Equal(2_600 - 510, starts[^1]);
        Assert.Equal(starts.Order(), starts);
        Assert.Equal(starts.Distinct(), starts);
    }

    [Fact]
    public void Starts_SlightlyOverWindow_TwoWindowsAnchoredAtBothEnds()
    {
        var starts = TokenWindows.Starts(totalTokens: 300, windowSize: 256);

        Assert.Equal(0, starts[0]);
        Assert.Equal(300 - 256, starts[^1]);
    }
}
