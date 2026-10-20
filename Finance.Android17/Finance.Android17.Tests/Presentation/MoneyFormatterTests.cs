using Finance.Android17.Presentation;
using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Presentation;

public class MoneyFormatterTests
{
    [Theory]
    [InlineData(0, "EUR", "0,00 €")]
    [InlineData(123_456, "EUR", "1 234,56 €")]
    [InlineData(-1_250, "EUR", "-12,50 €")]
    [InlineData(1_000, "USD", "10,00 $")]
    [InlineData(1_000, "gbp", "10,00 £")]
    [InlineData(1_000, "CHF", "10,00 CHF")]
    [InlineData(1_000, "CAD", "10,00 CA$")]
    [InlineData(1_000, "JPY", "10,00 ¥")]
    [InlineData(1_000, "XYZ", "10,00 €")]
    public void Money_UsesFrenchFormatting_AndTheCurrencySymbol(long minor, string currency, string expected) =>
        Assert.Equal(expected, MoneyFormatter.Money(minor, currency).Fr());

    [Fact]
    public void Money_DefaultsToEuros() =>
        Assert.Equal("5,00 €", MoneyFormatter.Money(500).Fr());

    [Fact]
    public void Money_IgnoresTheCurrentCulture()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
            Assert.Equal("1 234,56 €", MoneyFormatter.Money(123_456).Fr());
            Assert.Equal("02 oct. 2026", MoneyFormatter.Date(new DateTime(2026, 10, 2)));
        }
        finally { Thread.CurrentThread.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData(1_250, "12,50")]
    [InlineData(5, "0,05")]
    [InlineData(20_000, "200,00")]
    public void FixedAmount_AlwaysHasTwoDecimals(long minor, string expected) =>
        Assert.Equal(expected, MoneyFormatter.FixedAmount(minor));

    [Theory]
    [InlineData(1_250, "12,5")]
    [InlineData(20_000, "200")]
    [InlineData(5, "0,05")]
    [InlineData(0, "0")]
    public void PlainAmount_DropsUselessDecimals(long minor, string expected) =>
        Assert.Equal(expected, MoneyFormatter.PlainAmount(minor));

    [Theory]
    [InlineData(0.8, "80%")]
    [InlineData(0, "0%")]
    [InlineData(1, "100%")]
    public void Percent_HasNoDecimals(double ratio, string expected) =>
        Assert.Equal(expected, MoneyFormatter.Percent(ratio).Compact());

    [Fact]
    public void Date_UsesAbbreviatedFrenchMonths()
    {
        Assert.Equal("02 oct. 2026", MoneyFormatter.Date(new DateTime(2026, 10, 2)));
        Assert.Equal("15 févr. 2027", MoneyFormatter.Date(new DateTime(2027, 2, 15)));
    }

    [Fact]
    public void Hidden_IsFourBullets() =>
        Assert.Equal("••••", MoneyFormatter.Hidden);
}
