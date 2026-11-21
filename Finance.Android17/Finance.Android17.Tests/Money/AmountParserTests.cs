using Finance.Android17.Application;

namespace Finance.Android17.Tests.Money;

public class AmountParserTests
{
    [Theory]
    [InlineData("12,5", 1_250)]
    [InlineData("12.50", 1_250)]
    [InlineData(" 3 ", 300)]
    [InlineData("0", 0)]
    [InlineData("-3", -300)]
    [InlineData("1234,567", 123_457)]
    [InlineData("0,005", 1)]
    [InlineData("-0,005", -1)]
    [InlineData("0,004", 0)]
    public void TryParse_ReadsCommaOrDotAndRoundsAwayFromZero(string text, long expected)
    {
        Assert.True(AmountParser.TryParse(text, out var minor));
        Assert.Equal(expected, minor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("12,5 €")]
    [InlineData("1 234")]
    [InlineData("1,234.56")]
    public void TryParse_RejectsAnythingThatIsNotAnAmount(string? text)
    {
        Assert.False(AmountParser.TryParse(text, out var minor));
        Assert.Equal(0, minor);
    }

    [Fact]
    public void TryParse_RejectsAmountsThatWouldOverflowTheCentCount()
    {
        Assert.True(AmountParser.TryParse("100000000000", out var largest));
        Assert.Equal(InputLimits.MaxAmountMinor, largest);
        Assert.True(AmountParser.TryParse("-100000000000", out var smallest));
        Assert.Equal(-InputLimits.MaxAmountMinor, smallest);

        Assert.False(AmountParser.TryParse("100000000000,01", out _));
        Assert.False(AmountParser.TryParse("-100000000000,01", out _));
        Assert.False(AmountParser.TryParse("92233720368547759", out _));
        Assert.False(AmountParser.TryParse("79228162514264337593543950335", out _));
    }

    [Fact]
    public void TryParse_DoesNotDependOnTheCurrentCulture()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("de-DE");
            Assert.True(AmountParser.TryParse("1.5", out var minor));
            Assert.Equal(150, minor);
        }
        finally { Thread.CurrentThread.CurrentCulture = previous; }
    }
}
