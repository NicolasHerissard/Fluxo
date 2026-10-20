using Finance.Android17.Application;
using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Subscriptions;

public class SubscriptionCostsTests
{
    private static readonly DateTime Due = new(2026, 11, 1);

    [Fact]
    public void Monthly_SpreadsEachSubscriptionOverItsBillingInterval()
    {
        var subscriptions = new[]
        {
            Samples.Subscription(1_599, Due, 1),
            Samples.Subscription(3_000, Due, 3),
            Samples.Subscription(12_000, Due, 12)
        };

        Assert.Equal(1_599 + 1_000 + 1_000, SubscriptionCosts.MonthlyMinor(subscriptions));
    }

    [Fact]
    public void Annual_IsTwelveMonths() =>
        Assert.Equal(12 * 1_599, SubscriptionCosts.AnnualMinor([Samples.Subscription(1_599, Due)]));

    [Fact]
    public void Costs_AreZero_WithoutSubscriptions()
    {
        Assert.Equal(0, SubscriptionCosts.MonthlyMinor([]));
        Assert.Equal(0, SubscriptionCosts.AnnualMinor([]));
    }

    [Fact]
    public void Monthly_TreatsAnInvalidIntervalAsMonthly() =>
        Assert.Equal(500, SubscriptionCosts.MonthlyMinor([Samples.Subscription(500, Due, 0)]));
}
