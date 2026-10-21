using Finance.Android17.Application;
using Finance.Android17.Domain;
using Microsoft.EntityFrameworkCore;

namespace Finance.Android17.Infrastructure;

internal sealed class IncomeRepository(FinanceDatabase database) : Repository<Income>(database), IIncomeRepository
{
    protected override DbSet<Income> Set(FinanceDbContext db) => db.Incomes;

    public async Task<IReadOnlyList<Income>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await Database.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await db.Incomes.AsNoTracking().Where(x => x.DeletedAtUtc == null)
            .OrderByDescending(x => x.OccurredOn).ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task AddAsync(long amountMinor, string description, DateTime occurredOn, string currencyCode = "EUR", CancellationToken cancellationToken = default) =>
        InsertAsync(now => new Income
        {
            AmountMinor = amountMinor,
            Description = description.Trim(),
            OccurredOn = occurredOn.Date,
            CurrencyCode = currencyCode,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        }, cancellationToken);

    public Task UpdateAsync(Guid id, long amountMinor, string description, DateTime occurredOn, string currencyCode = "EUR", CancellationToken cancellationToken = default) =>
        ChangeAsync(id, income =>
        {
            income.AmountMinor = amountMinor;
            income.Description = description.Trim();
            income.OccurredOn = occurredOn.Date;
            income.CurrencyCode = currencyCode;
        }, cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => MarkDeletedAsync(id, cancellationToken);
}
