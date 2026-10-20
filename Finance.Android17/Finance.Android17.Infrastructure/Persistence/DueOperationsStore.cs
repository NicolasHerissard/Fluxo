using Finance.Android17.Application;
using Finance.Android17.Domain;
using Microsoft.EntityFrameworkCore;

namespace Finance.Android17.Infrastructure;

internal sealed class DueOperationsStore(FinanceDatabase database) : IDueOperationsStore
{
    public async Task ApplyAsync(DueOperationsPlan plan, CancellationToken cancellationToken = default)
    {
        if (plan.IsEmpty) return;
        await using var db = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var now = database.UtcNow;

        if (plan.Expenses.Count > 0)
        {
            var category = await db.Categories.FirstOrDefaultAsync(x => x.DeletedAtUtc == null && x.Name == DefaultCategories.SubscriptionsName, cancellationToken).ConfigureAwait(false);
            if (category is null)
            {
                category = DefaultCategories.CreateSubscriptions();
                category.CreatedAtUtc = category.UpdatedAtUtc = now;
                db.Categories.Add(category);
            }
            foreach (var expense in plan.Expenses)
            {
                expense.Category = category;
                expense.CategoryId = category.Id;
                db.Expenses.Add(expense);
            }
        }
        db.Incomes.AddRange(plan.Incomes);

        foreach (var advance in plan.SubscriptionAdvances)
        {
            var subscription = await db.Subscriptions.FindAsync([advance.Id], cancellationToken).ConfigureAwait(false);
            if (subscription is null) continue;
            subscription.NextBillingDate = advance.NextDate.Date;
            subscription.Touch(now);
        }
        foreach (var advance in plan.RecurringIncomeAdvances)
        {
            var recurring = await db.RecurringIncomes.FindAsync([advance.Id], cancellationToken).ConfigureAwait(false);
            if (recurring is null) continue;
            recurring.NextOccurrenceDate = advance.NextDate.Date;
            recurring.Touch(now);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
