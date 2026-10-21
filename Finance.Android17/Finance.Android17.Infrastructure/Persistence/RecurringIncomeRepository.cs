using Finance.Android17.Application;
using Finance.Android17.Domain;
using Microsoft.EntityFrameworkCore;

namespace Finance.Android17.Infrastructure;

internal sealed class RecurringIncomeRepository(FinanceDatabase database) : Repository<RecurringIncome>(database), IRecurringIncomeRepository
{
    protected override DbSet<RecurringIncome> Set(FinanceDbContext db) => db.RecurringIncomes;

    public async Task<IReadOnlyList<RecurringIncome>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await Database.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await db.RecurringIncomes.AsNoTracking().Where(x => x.DeletedAtUtc == null).OrderBy(x => x.NextOccurrenceDate)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task AddAsync(string name, long amountMinor, int intervalMonths, DateTime nextOccurrenceDate, string currencyCode = "EUR", CancellationToken cancellationToken = default) =>
        InsertAsync(now => new RecurringIncome
        {
            Name = name.Trim(),
            AmountMinor = amountMinor,
            CurrencyCode = currencyCode,
            IntervalMonths = Math.Max(1, intervalMonths),
            NextOccurrenceDate = nextOccurrenceDate.Date,
            AnchorDay = nextOccurrenceDate.Day,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        }, cancellationToken);

    public Task UpdateAsync(Guid id, string name, long amountMinor, int intervalMonths, DateTime nextOccurrenceDate, string currencyCode = "EUR", CancellationToken cancellationToken = default) =>
        ChangeAsync(id, recurring =>
        {
            recurring.Name = name.Trim();
            recurring.AmountMinor = amountMinor;
            recurring.CurrencyCode = currencyCode;
            recurring.IntervalMonths = Math.Max(1, intervalMonths);
            // Une date inchangée garde son jour d'ancrage ; une date choisie le redéfinit.
            if (recurring.NextOccurrenceDate != nextOccurrenceDate.Date || recurring.AnchorDay <= 0)
                recurring.AnchorDay = nextOccurrenceDate.Day;
            recurring.NextOccurrenceDate = nextOccurrenceDate.Date;
        }, cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => MarkDeletedAsync(id, cancellationToken);
}
