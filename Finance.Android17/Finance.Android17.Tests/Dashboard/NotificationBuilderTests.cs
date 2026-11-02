using Finance.Android17.Application;
using Finance.Android17.Domain;
using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Dashboard;

public class NotificationBuilderTests
{
    private static readonly DateTime Today = new(2026, 10, 20);

    private static FinanceData Data(
        IEnumerable<Expense>? expenses = null,
        IEnumerable<Subscription>? subscriptions = null,
        IEnumerable<Income>? incomes = null,
        long initialBalance = 0) =>
        new([.. expenses ?? []], [.. incomes ?? []], [.. subscriptions ?? []], [], initialBalance);

    [Fact]
    public void Build_WithNothingToReport_ReturnsNoAlert()
    {
        var data = Data([Samples.Expense(1_000, new DateTime(2026, 10, 2))], incomes: [Samples.Income(100_000, new DateTime(2026, 10, 1))]);

        Assert.Empty(NotificationBuilder.Build(data, Today, monthlyBudgetMinor: 100_000));
    }

    [Fact]
    public void Build_WarnsAt80Percent_AndCriticallyWhenTheMonthlyBudgetIsExceeded()
    {
        var data = Data([Samples.Expense(8_500, new DateTime(2026, 10, 2))], incomes: [Samples.Income(100_000, new DateTime(2026, 10, 1))]);

        var warning = Assert.Single(NotificationBuilder.Build(data, Today, 10_000));
        Assert.Equal(NotificationTopic.MonthlyBudget, warning.Topic);
        Assert.Equal(NotificationSeverity.Warning, warning.Severity);
        Assert.Equal((8_500, 10_000), (warning.AmountMinor, warning.LimitMinor));

        var exceeded = Assert.Single(NotificationBuilder.Build(data, Today, 8_000));
        Assert.Equal(NotificationSeverity.Critical, exceeded.Severity);
        Assert.NotEqual(warning.Key, exceeded.Key);
    }

    [Fact]
    public void Build_IgnoresTheMonthlyBudget_WhenItIsNotSet()
    {
        var data = Data([Samples.Expense(8_500, new DateTime(2026, 10, 2))], incomes: [Samples.Income(100_000, new DateTime(2026, 10, 1))]);

        Assert.Empty(NotificationBuilder.Build(data, Today, 0));
    }

    [Fact]
    public void Build_ReportsACategoryBudgetThatIsClose()
    {
        var food = Samples.Category("Alimentation", budgetMinor: 10_000);
        var data = Data([Samples.Expense(9_000, new DateTime(2026, 10, 2), food)], incomes: [Samples.Income(100_000, new DateTime(2026, 10, 1))]);

        var alert = Assert.Single(NotificationBuilder.Build(data, Today, 0));

        Assert.Equal(NotificationTopic.CategoryBudget, alert.Topic);
        Assert.Equal("Alimentation", alert.Subject);
        Assert.Equal((9_000, 10_000), (alert.AmountMinor, alert.LimitMinor));
    }

    [Fact]
    public void Build_ReportsAProbableOverdraft_First()
    {
        var data = Data([Samples.Expense(10_000, new DateTime(2026, 10, 2))], [Samples.Subscription(1_599, new DateTime(2026, 10, 22), name: "Netflix")]);

        var alerts = NotificationBuilder.Build(data, Today, 0);

        Assert.Equal([NotificationTopic.Overdraft, NotificationTopic.SubscriptionDue], alerts.Select(x => x.Topic));
        Assert.Equal(NotificationSeverity.Critical, alerts[0].Severity);
        Assert.True(alerts[0].AmountMinor < 0);
    }

    [Fact]
    public void Build_ListsTheSubscriptionsDueWithinAWeek_ByDate()
    {
        var data = Data(
            subscriptions:
            [
                Samples.Subscription(999, new DateTime(2026, 10, 25), name: "Musique"),
                Samples.Subscription(1_599, new DateTime(2026, 10, 20), name: "Vidéo"),
                Samples.Subscription(500, new DateTime(2026, 10, 27), name: "Cloud"),
                Samples.Subscription(500, new DateTime(2026, 10, 28), name: "Trop tard"),
                Samples.Subscription(500, new DateTime(2026, 10, 19), name: "Déjà passé")
            ],
            incomes: [Samples.Income(100_000, new DateTime(2026, 10, 1))]);

        var alerts = NotificationBuilder.Build(data, Today, 0);

        Assert.Equal(["Vidéo", "Musique", "Cloud"], alerts.Select(x => x.Subject));
        Assert.Equal([0, 5, 7], alerts.Select(x => x.DaysAway));
        Assert.All(alerts, x => Assert.Equal(NotificationSeverity.Info, x.Severity));
    }

    [Fact]
    public void Build_Keys_ChangeWithTheDueDate_SoARenewedSubscriptionIsNewAgain()
    {
        var subscription = Samples.Subscription(999, new DateTime(2026, 10, 22), name: "Musique");
        var income = Samples.Income(100_000, new DateTime(2026, 10, 1));

        var before = Assert.Single(NotificationBuilder.Build(Data(subscriptions: [subscription], incomes: [income]), Today, 0));
        subscription.NextBillingDate = new DateTime(2026, 11, 22);
        var after = NotificationBuilder.Build(Data(subscriptions: [subscription], incomes: [income]), new DateTime(2026, 11, 18), 0);

        Assert.NotEqual(before.Key, Assert.Single(after).Key);
    }
}
