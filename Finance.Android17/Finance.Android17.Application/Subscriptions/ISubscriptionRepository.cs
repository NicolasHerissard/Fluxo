using Finance.Android17.Domain;

namespace Finance.Android17.Application;

public interface ISubscriptionRepository
{
    /// <summary>Abonnements non supprimés, par prochaine échéance.</summary>
    Task<IReadOnlyList<Subscription>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <param name="billingIntervalMonths">Au moins 1 : une valeur inférieure est ramenée à 1.</param>
    Task AddAsync(string name, long amountMinor, int billingIntervalMonths, DateTime nextBillingDate, string currencyCode = CurrencyCodes.Default, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, string name, long amountMinor, int billingIntervalMonths, DateTime nextBillingDate, string currencyCode = CurrencyCodes.Default, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Annule une suppression logique (annulation juste après la suppression).</summary>
    Task RestoreAsync(Guid id, CancellationToken cancellationToken = default);
}
