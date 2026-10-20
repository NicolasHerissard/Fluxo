using Finance.Android17.Domain;

namespace Finance.Android17.Application;

/// <summary>
/// Persistance des dépenses. Les descriptions sont nettoyées (espaces en début et fin) et les dates ramenées à
/// minuit. Une suppression est logique : la dépense disparaît des listes mais peut être restaurée.
/// </summary>
public interface IExpenseRepository
{
    /// <summary>Dépenses non supprimées, avec leur catégorie, de la plus récente à la plus ancienne.</summary>
    Task<IReadOnlyList<Expense>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(long amountMinor, string description, DateTime occurredOn, Guid categoryId, string currencyCode = "EUR", CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, long amountMinor, string description, DateTime occurredOn, Guid categoryId, string currencyCode = "EUR", CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task RestoreAsync(Guid id, CancellationToken cancellationToken = default);
}
