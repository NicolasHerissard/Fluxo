using Finance.Android17.Application;
using Finance.Android17.Domain;
using Microsoft.EntityFrameworkCore;

namespace Finance.Android17.Infrastructure;

internal sealed class CategoryRepository(FinanceDatabase database) : Repository<Category>(database), ICategoryRepository
{
    protected override DbSet<Category> Set(FinanceDbContext db) => db.Categories;

    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await Database.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await db.Categories.AsNoTracking().Where(x => x.DeletedAtUtc == null).OrderBy(x => x.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task AddAsync(string name, string color, long? budgetMinor = null, CancellationToken cancellationToken = default) =>
        InsertAsync(now => new Category { Name = name.Trim(), Color = color, BudgetMinor = budgetMinor, CreatedAtUtc = now, UpdatedAtUtc = now }, cancellationToken);

    public Task UpdateAsync(Guid id, string name, string color, long? budgetMinor, CancellationToken cancellationToken = default) =>
        ChangeAsync(id, category =>
        {
            category.Name = name.Trim();
            category.Color = color;
            category.BudgetMinor = budgetMinor;
        }, cancellationToken);

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using (var db = await Database.OpenAsync(cancellationToken).ConfigureAwait(false))
        {
            if (await db.Expenses.AnyAsync(x => x.CategoryId == id && x.DeletedAtUtc == null, cancellationToken).ConfigureAwait(false))
                throw new InvalidOperationException("Cette catégorie est utilisée par une transaction.");
        }
        await MarkDeletedAsync(id, cancellationToken).ConfigureAwait(false);
    }
}
