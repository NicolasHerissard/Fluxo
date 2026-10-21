using Finance.Android17.Domain;

namespace Finance.Android17.Application;

public static class SubscriptionCosts
{
    public static long MonthlyMinor(IEnumerable<Subscription> subscriptions) => subscriptions.Sum(x => x.MonthlyEquivalentMinor);

    public static long AnnualMinor(IEnumerable<Subscription> subscriptions) => MonthlyMinor(subscriptions) * 12;
}
