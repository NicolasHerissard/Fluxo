using Finance.Android17.Application;
using Finance.Android17.Domain;
using Microsoft.EntityFrameworkCore;

namespace Finance.Android17.Infrastructure;

/// <summary>Instantané, restauration et remise à zéro de toutes les données.</summary>
internal sealed class FinanceDataStore(FinanceDatabase database) : IFinanceDataStore
{
    /// <summary>Format écrit par <see cref="CreateSnapshotAsync"/> ; les formats 1 et 2 sont acceptés à la restauration.</summary>
    public const int SnapshotFormatVersion = 2;

    public async Task<FinanceBackupSnapshot> CreateSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
        var categories = await db.Categories.AsNoTracking().Where(x => x.DeletedAtUtc == null).Select(x => new CategoryBackup(x.Id, x.Name, x.Color, x.BudgetMinor, x.CreatedAtUtc)).ToListAsync(cancellationToken).ConfigureAwait(false);
        var expenses = await db.Expenses.AsNoTracking().Where(x => x.DeletedAtUtc == null).Select(x => new ExpenseBackup(x.Id, x.AmountMinor, x.CurrencyCode, x.Description, x.OccurredOn, x.CategoryId, x.CreatedAtUtc)).ToListAsync(cancellationToken).ConfigureAwait(false);
        var subscriptions = await db.Subscriptions.AsNoTracking().Where(x => x.DeletedAtUtc == null).Select(x => new SubscriptionBackup(x.Id, x.Name, x.AmountMinor, x.CurrencyCode, x.BillingIntervalMonths, x.NextBillingDate, x.ReminderEnabled, x.CreatedAtUtc)).ToListAsync(cancellationToken).ConfigureAwait(false);
        var incomes = await db.Incomes.AsNoTracking().Where(x => x.DeletedAtUtc == null).Select(x => new IncomeBackup(x.Id, x.AmountMinor, x.CurrencyCode, x.Description, x.OccurredOn, x.RecurringIncomeId, x.CreatedAtUtc)).ToListAsync(cancellationToken).ConfigureAwait(false);
        var recurringIncomes = await db.RecurringIncomes.AsNoTracking().Where(x => x.DeletedAtUtc == null).Select(x => new RecurringIncomeBackup(x.Id, x.Name, x.AmountMinor, x.CurrencyCode, x.IntervalMonths, x.NextOccurrenceDate, x.CreatedAtUtc)).ToListAsync(cancellationToken).ConfigureAwait(false);
        var savingsGoals = await db.SavingsGoals.AsNoTracking().Where(x => x.DeletedAtUtc == null).Select(x => new SavingsGoalBackup(x.Id, x.Name, x.TargetAmountMinor, x.SavedAmountMinor, x.TargetDate, x.CreatedAtUtc)).ToListAsync(cancellationToken).ConfigureAwait(false);
        var initialBalance = await db.FinancialProfiles.AsNoTracking().Where(x => x.Id == FinancialProfile.SingletonId).Select(x => x.InitialBalanceMinor).SingleAsync(cancellationToken).ConfigureAwait(false);
        return new FinanceBackupSnapshot(SnapshotFormatVersion, database.UtcNow, categories, expenses, subscriptions, incomes, recurringIncomes, savingsGoals, initialBalance);
    }

    public async Task RestoreSnapshotAsync(FinanceBackupSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        if (snapshot.FormatVersion is not (1 or 2)) throw new InvalidOperationException("Format de sauvegarde non pris en charge.");
        await using var db = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await RemoveAllAsync(db, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var now = database.UtcNow;
        db.Categories.AddRange(snapshot.Categories.Select(x => new Category { Id = x.Id, Name = x.Name, Color = x.Color, BudgetMinor = x.BudgetMinor, CreatedAtUtc = x.CreatedAtUtc, UpdatedAtUtc = now }));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        db.Expenses.AddRange(snapshot.Expenses.Select(x => new Expense { Id = x.Id, AmountMinor = x.AmountMinor, CurrencyCode = x.CurrencyCode, Description = x.Description, OccurredOn = x.OccurredOn, CategoryId = x.CategoryId, CreatedAtUtc = x.CreatedAtUtc, UpdatedAtUtc = now }));
        db.Subscriptions.AddRange(snapshot.Subscriptions.Select(x => new Subscription { Id = x.Id, Name = x.Name, AmountMinor = x.AmountMinor, CurrencyCode = x.CurrencyCode, BillingIntervalMonths = x.BillingIntervalMonths, NextBillingDate = x.NextBillingDate, ReminderEnabled = x.ReminderEnabled, CreatedAtUtc = x.CreatedAtUtc, UpdatedAtUtc = now }));
        db.Incomes.AddRange((snapshot.Incomes ?? []).Select(x => new Income { Id = x.Id, AmountMinor = x.AmountMinor, CurrencyCode = x.CurrencyCode, Description = x.Description, OccurredOn = x.OccurredOn, RecurringIncomeId = x.RecurringIncomeId, CreatedAtUtc = x.CreatedAtUtc, UpdatedAtUtc = now }));
        db.RecurringIncomes.AddRange((snapshot.RecurringIncomes ?? []).Select(x => new RecurringIncome { Id = x.Id, Name = x.Name, AmountMinor = x.AmountMinor, CurrencyCode = x.CurrencyCode, IntervalMonths = x.IntervalMonths, NextOccurrenceDate = x.NextOccurrenceDate, CreatedAtUtc = x.CreatedAtUtc, UpdatedAtUtc = now }));
        db.SavingsGoals.AddRange((snapshot.SavingsGoals ?? []).Select(x => new SavingsGoal { Id = x.Id, Name = x.Name, TargetAmountMinor = x.TargetAmountMinor, SavedAmountMinor = x.SavedAmountMinor, TargetDate = x.TargetDate, CreatedAtUtc = x.CreatedAtUtc, UpdatedAtUtc = now }));
        var profile = await db.FinancialProfiles.SingleAsync(x => x.Id == FinancialProfile.SingletonId, cancellationToken).ConfigureAwait(false);
        profile.InitialBalanceMinor = snapshot.FormatVersion >= 2 ? snapshot.InitialBalanceMinor : 0;
        profile.UpdatedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await RemoveAllAsync(db, cancellationToken).ConfigureAwait(false);
        var profile = await db.FinancialProfiles.SingleAsync(x => x.Id == FinancialProfile.SingletonId, cancellationToken).ConfigureAwait(false);
        profile.InitialBalanceMinor = 0;
        profile.UpdatedAtUtc = database.UtcNow;
        db.Categories.AddRange(DefaultCategories.CreateInitialSet());
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task RemoveAllAsync(FinanceDbContext db, CancellationToken cancellationToken)
    {
        db.Expenses.RemoveRange(await db.Expenses.ToListAsync(cancellationToken).ConfigureAwait(false));
        db.Subscriptions.RemoveRange(await db.Subscriptions.ToListAsync(cancellationToken).ConfigureAwait(false));
        db.Incomes.RemoveRange(await db.Incomes.ToListAsync(cancellationToken).ConfigureAwait(false));
        db.RecurringIncomes.RemoveRange(await db.RecurringIncomes.ToListAsync(cancellationToken).ConfigureAwait(false));
        db.SavingsGoals.RemoveRange(await db.SavingsGoals.ToListAsync(cancellationToken).ConfigureAwait(false));
        db.Categories.RemoveRange(await db.Categories.ToListAsync(cancellationToken).ConfigureAwait(false));
    }
}
