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

    private const string CarrefourTicket = """
        Bienvenue chez
        Carrefour
        ** LAON **
        TEL 03.23.26.79.79
        DESIGNATION P.U x QTE MONTANT€
        4 BARRES PROTEIN CHO 2,75x3 8,25
        4 1KG RIZ BASMATI CR 2,59
        4 850G SKYR NATURE C 2,75
        Total Alimentaire 42,18
        6 4 CR2016 ENERGIZER 9,99
        Total Non Alimentaire 9,99
        15 ARTICLES TOTAL A PAYER 52,17
        € Cartes Bancaires 52,17
        TVA 4: 5,50% € 39,98 2,20
        TVA 6:20,00% € 8,33 1,67
        ==== TOTAL TVA € 3,86
        """;

    [Fact]
    public void Parse_CarrefourTicket_ReturnsTheTotalToPay()
    {
        var result = ReceiptParser.Parse(CarrefourTicket);

        Assert.Equal(5217, result.AmountMinor);
        Assert.Equal("Carrefour", result.MerchantName);
    }

    [Fact]
    public void Parse_CarrefourTicket_WhenOcrSplitsLabelsAndAmounts_StillFindsTheTotal()
    {
        var result = ReceiptParser.Parse("Carrefour\nTotal Alimentaire\n42,18\nTotal Non Alimentaire\n9,99\nTOTAL A PAYER\n52,17\nCartes Bancaires\n52,17\nTVA 5,50%\n39,98\n2,20");

        Assert.Equal(5217, result.AmountMinor);
    }

    [Fact]
    public void Parse_UsesTheCardPayment_WhenNoPayableLabelIsRead()
    {
        var result = ReceiptParser.Parse("LECLERC\nTotal Alimentaire 30,00\nCARTE BANCAIRE 52,17\nTVA 20,00% 43,47 8,70");

        Assert.Equal(5217, result.AmountMinor);
    }

    [Fact]
    public void Parse_UsesRepeatedAmount_WhenNoLabelIsRead()
    {
        var result = ReceiptParser.Parse("MAGASIN\nPain 2,50\nLait 1,20\n52,17\n52,17\nTVA 3,86");

        Assert.Equal(5217, result.AmountMinor);
    }

    [Theory]
    [InlineData("LIDL\nBanane 1,99\nTOTAL 23,45\nEspèces 50,00\nRendu monnaie 26,55", 2345)]
    [InlineData("AUCHAN\nMONTANT DÛ 18,90 EUR\nCB 18,90", 1890)]
    [InlineData("RESTAURANT LE PORT\nSous-total 40,00\nNET A PAYER 36,00\nRemise 4,00\nVisa 36,00", 3600)]
    [InlineData("DECATHLON\nTOTAL TTC 89,90\nDont TVA 20% 14,98\nPaiement carte 89,90", 8990)]
    [InlineData("MONOPRIX\nTOTAL REMISE 3,00\nTOTAL A REGLER 27,40", 2740)]
    public void Parse_FindsThePaidAmountOnOtherRetailers(string text, long expected)
    {
        Assert.Equal(expected, ReceiptParser.Parse(text).AmountMinor);
    }

    [Fact]
    public void Parse_SkipsGreetingLinesWhenLookingForTheMerchant()
    {
        Assert.Equal("Carrefour", ReceiptParser.Parse("Bienvenue chez\nCarrefour\nTOTAL A PAYER 5,00").MerchantName);
    }
}
