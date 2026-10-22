using Finance.Android17.Domain;

namespace Finance.Android17.Application;

public interface ISavingsGoalRepository
{
    /// <summary>Objectifs non supprimés : ceux sans date d'abord, puis par date cible et par nom.</summary>
    Task<IReadOnlyList<SavingsGoal>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(string name, long targetAmountMinor, long savedAmountMinor, DateTime? targetDate, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, string name, long targetAmountMinor, long savedAmountMinor, DateTime? targetDate, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Annule une suppression logique (annulation juste après la suppression).</summary>
    Task RestoreAsync(Guid id, CancellationToken cancellationToken = default);
}
