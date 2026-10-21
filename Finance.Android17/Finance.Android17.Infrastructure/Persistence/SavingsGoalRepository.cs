using Finance.Android17.Application;
using Finance.Android17.Domain;
using Microsoft.EntityFrameworkCore;

namespace Finance.Android17.Infrastructure;

internal sealed class SavingsGoalRepository(FinanceDatabase database) : Repository<SavingsGoal>(database), ISavingsGoalRepository
{
    protected override DbSet<SavingsGoal> Set(FinanceDbContext db) => db.SavingsGoals;

    public async Task<IReadOnlyList<SavingsGoal>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await Database.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await db.SavingsGoals.AsNoTracking().Where(x => x.DeletedAtUtc == null)
            .OrderBy(x => x.TargetDate).ThenBy(x => x.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task AddAsync(string name, long targetAmountMinor, long savedAmountMinor, DateTime? targetDate, CancellationToken cancellationToken = default) =>
        InsertAsync(now => new SavingsGoal
        {
            Name = name.Trim(),
            TargetAmountMinor = targetAmountMinor,
            SavedAmountMinor = savedAmountMinor,
            TargetDate = targetDate?.Date,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        }, cancellationToken);

    public Task UpdateAsync(Guid id, string name, long targetAmountMinor, long savedAmountMinor, DateTime? targetDate, CancellationToken cancellationToken = default) =>
        ChangeAsync(id, goal =>
        {
            goal.Name = name.Trim();
            goal.TargetAmountMinor = targetAmountMinor;
            goal.SavedAmountMinor = savedAmountMinor;
            goal.TargetDate = targetDate?.Date;
        }, cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => MarkDeletedAsync(id, cancellationToken);
}
