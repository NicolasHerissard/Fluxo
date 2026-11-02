using Finance.Android17.Application;
using Finance.Android17.Presentation;
using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Presentation;

public sealed class NotificationTextsTests
{
    private static AppNotification Overdraft => new("overdraft:202610", NotificationTopic.Overdraft, NotificationSeverity.Critical, string.Empty, -12_345, 0, 0);

    [Fact]
    public void Compose_WritesTheOverdraftAlert_WithTheAmount()
    {
        var text = NotificationTexts.Compose(Overdraft, NotificationTexts.Amounts(hideAmounts: false, "EUR"));

        Assert.Equal("Découvert probable en fin de mois", text.Title);
        Assert.Contains("123,45", text.Text.Compact());
        Assert.Equal("trending-down", text.Icon);
    }

    [Fact]
    public void Compose_MasksTheAmounts_WhenTheyAreHidden()
    {
        var text = NotificationTexts.Compose(Overdraft, NotificationTexts.Amounts(hideAmounts: true, "EUR"));

        Assert.Equal("Solde estimé au dernier jour du mois : ••••.", text.Text);
    }

    [Theory]
    [InlineData(NotificationSeverity.Warning, "Budget « Alimentation » bientôt atteint")]
    [InlineData(NotificationSeverity.Critical, "Budget « Alimentation » dépassé")]
    public void Compose_NamesTheCategory_AndItsState(NotificationSeverity severity, string expectedTitle)
    {
        var alert = new AppNotification("category:Alimentation:202610:X", NotificationTopic.CategoryBudget, severity, "Alimentation", 16_250, 20_000, 0);

        var text = NotificationTexts.Compose(alert, NotificationTexts.Amounts(false, "EUR"));

        Assert.Equal(expectedTitle, text.Title);
        Assert.Equal("wallet", text.Icon);
    }

    [Theory]
    [InlineData(0, "Prélèvement aujourd’hui")]
    [InlineData(1, "Prélèvement demain")]
    [InlineData(5, "Prélèvement dans 5 jours")]
    public void Compose_AnnouncesAnUpcomingDebit_ByHowFarItIs(int days, string expectedTitle)
    {
        var alert = new AppNotification("subscription:x", NotificationTopic.SubscriptionDue, NotificationSeverity.Info, "Netflix", 1_599, 0, days);

        var text = NotificationTexts.Compose(alert, NotificationTexts.Amounts(false, "EUR"));

        Assert.Equal(expectedTitle, text.Title);
        Assert.StartsWith("Netflix · ", text.Text);
        Assert.Equal("calendar", text.Icon);
    }
}
