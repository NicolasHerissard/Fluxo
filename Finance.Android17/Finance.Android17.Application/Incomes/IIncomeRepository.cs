using Finance.Android17.Domain;

namespace Finance.Android17.Application;

public interface IIncomeRepository
{
    /// <summary>Revenus non supprimés, du plus récent au plus ancien.</summary>
    Task<IReadOnlyList<Income>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(long amountMinor, string description, DateTime occurredOn, string currencyCode = "EUR", CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, long amountMinor, string description, DateTime occurredOn, string currencyCode = "EUR", CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Annule une suppression logique (annulation juste après la suppression).</summary>
    Task RestoreAsync(Guid id, CancellationToken cancellationToken = default);
}
