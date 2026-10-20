using Finance.Android17.Presentation;

namespace Finance.Android17.Tests.Presentation;

public class BillingFrequencyTests
{
    [Theory]
    [InlineData("Mensuel", 1)]
    [InlineData("Trimestriel", 3)]
    [InlineData("Annuel", 12)]
    [InlineData("n'importe quoi", 1)]
    public void ToMonths_MapsTheOptionToItsInterval(string option, int months) =>
        Assert.Equal(months, BillingFrequency.ToMonths(option));

    [Theory]
    [InlineData(1, "Mensuel")]
    [InlineData(3, "Trimestriel")]
    [InlineData(12, "Annuel")]
    [InlineData(6, "Mensuel")]
    [InlineData(0, "Mensuel")]
    public void ToOption_MapsTheIntervalBackToTheOption(int months, string option) =>
        Assert.Equal(option, BillingFrequency.ToOption(months));

    [Theory]
    [InlineData(1, "Mensuel")]
    [InlineData(3, "Tous les 3 mois")]
    [InlineData(12, "Annuel")]
    public void ToDisplay_ReadsNaturallyInLists(int months, string text) =>
        Assert.Equal(text, BillingFrequency.ToDisplay(months));

    [Fact]
    public void Options_AreTheThreeStoredFrequencies() =>
        Assert.Equal(["Mensuel", "Trimestriel", "Annuel"], BillingFrequency.Options);

    [Fact]
    public void EveryOption_RoundTripsThroughItsInterval()
    {
        foreach (var option in BillingFrequency.Options)
            Assert.Equal(option, BillingFrequency.ToOption(BillingFrequency.ToMonths(option)));
    }
}
