using Finance.Android17.Application;

namespace Finance.Android17.Tests.Receipts;

public class ReceiptParserTests
{
    [Fact]
    public void Parse_RecognizesFrenchTotalDateAndMerchant()
    {
        var result = ReceiptParser.Parse("CARREFOUR MARKET\nPain 2,50 €\nTOTAL TTC 12,50 €\n29/09/2026");

        Assert.Equal(1250, result.AmountMinor);
        Assert.Equal(new DateTime(2026, 9, 29), result.OccurredOn);
        Assert.Equal("CARREFOUR MARKET", result.MerchantName);
    }
}
