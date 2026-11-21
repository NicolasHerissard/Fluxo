using Finance.Android17.Domain;

namespace Finance.Android17.Application;

public static class SubscriptionCosts
{
    private const int MonthsPerYear = 12;

    public static long MonthlyMinor(IEnumerable<Subscription> subscriptions) => subscriptions.Sum(x => x.MonthlyEquivalentMinor);

    /// <summary>Coût annuel : calculé depuis chaque montant (et non depuis le mensuel tronqué) pour qu'un abonnement annuel de 59,99 € reste 59,99 €.</summary>
    public static long AnnualMinor(IEnumerable<Subscription> subscriptions) => subscriptions.Sum(AnnualMinor);

    private static long AnnualMinor(Subscription subscription)
    {
        var interval = subscription.BillingIntervalMonths;
        if (interval <= 0) return subscription.AmountMinor * MonthsPerYear;
        return (long)Math.Round((decimal)subscription.AmountMinor * MonthsPerYear / interval, MidpointRounding.AwayFromZero);
    }
}
