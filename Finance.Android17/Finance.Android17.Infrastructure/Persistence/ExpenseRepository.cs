using Finance.Android17.Application;
using Finance.Android17.Domain;
using Microsoft.EntityFrameworkCore;

namespace Finance.Android17.Infrastructure;

internal sealed class ExpenseRepository(FinanceDatabase database) : Repository<Expense>(database), IExpenseRepository
{
    protected override DbSet<Expense> Set(FinanceDbContext db) => db.Expenses;

    public async Task<IReadOnlyList<Expense>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await Database.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await db.Expenses.AsNoTracking().Include(x => x.Category).Where(x => x.DeletedAtUtc == null)
            .OrderByDescending(x => x.OccurredOn).ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task AddAsync(long amountMinor, string description, DateTime occurredOn, Guid categoryId, string currencyCode = CurrencyCodes.Default, CancellationToken cancellationToken = default) =>
        InsertAsync(now => new Expense
        {
            AmountMinor = amountMinor,
            CurrencyCode = currencyCode,
            Description = description.Trim(),
            OccurredOn = occurredOn.Date,
            CategoryId = categoryId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        }, cancellationToken);

    public Task UpdateAsync(Guid id, long amountMinor, string description, DateTime occurredOn, Guid categoryId, string currencyCode = CurrencyCodes.Default, CancellationToken cancellationToken = default) =>
        ChangeAsync(id, expense =>
        {
            expense.AmountMinor = amountMinor;
            expense.Description = description.Trim();
            expense.OccurredOn = occurredOn.Date;
            expense.CategoryId = categoryId;
            expense.CurrencyCode = currencyCode;
        }, cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => MarkDeletedAsync(id, cancellationToken);

    public Task RestoreAsync(Guid id, CancellationToken cancellationToken = default) => MarkRestoredAsync(id, cancellationToken);
}
