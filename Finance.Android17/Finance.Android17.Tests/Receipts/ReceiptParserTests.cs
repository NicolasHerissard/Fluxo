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

    [Fact]
    public void Parse_IgnoresVatAndSubtotalLines()
    {
        var result = ReceiptParser.Parse("BOULANGERIE DUPONT\nSOUS-TOTAL 10,00 €\nTOTAL TTC 10,50 €\nTVA 5,5% 0,55 €\n03/10/2026");

        Assert.Equal(1050, result.AmountMinor);
    }

    [Fact]
    public void Parse_FallbackSkipsVatAndChangeLines()
    {
        var result = ReceiptParser.Parse("PHARMACIE\nDoliprane 3,20\nTVA 0,07\nRendu 6,80");

        Assert.Equal(320, result.AmountMinor);
    }

    [Fact]
    public void Parse_ReadsAmountOnNextLineAfterTotalLabel()
    {
        var result = ReceiptParser.Parse("LECLERC\nTOTAL A PAYER\n1 234,56 €\n");

        Assert.Equal(123456, result.AmountMinor);
    }

    [Theory]
    [InlineData("MAGASIN\nTOTAL 5,00\n29/09/26", 2026, 9, 29)]
    [InlineData("MAGASIN\nTOTAL 5,00\n29.09.2026", 2026, 9, 29)]
    [InlineData("MAGASIN\nTOTAL 5,00\n29-09-2026", 2026, 9, 29)]
    public void Parse_AcceptsCommonDateFormats(string text, int year, int month, int day)
    {
        Assert.Equal(new DateTime(year, month, day), ReceiptParser.Parse(text).OccurredOn);
    }

    [Fact]
    public void Parse_ReturnsNothingForUnreadableText()
    {
        var result = ReceiptParser.Parse("???\n---");

        Assert.Null(result.AmountMinor);
        Assert.Null(result.OccurredOn);
        Assert.Null(result.MerchantName);
    }
}
