using Finance.Android17.Application;
using Finance.Android17.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Finance.Android17.Infrastructure;

public sealed class EncryptedFinanceStore(ISecretStore secrets, FinanceDatabaseOptions options) : IFinanceStore
{
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private string? _connectionString;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_connectionString is not null) return;
        await _initializationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_connectionString is not null) return;
            SQLitePCL.Batteries_V2.Init();
            var key = await secrets.GetOrCreateDatabaseKeyAsync(cancellationToken).ConfigureAwait(false);
            var dataDirectory = Path.GetDirectoryName(Path.GetFullPath(options.DatabasePath));
            if (!string.IsNullOrEmpty(dataDirectory)) Directory.CreateDirectory(dataDirectory);
            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = options.DatabasePath,
                Password = key,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();
            await using var db = CreateContext();
            await db.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "Subscriptions" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_Subscriptions" PRIMARY KEY,
                    "Name" TEXT NOT NULL,
                    "AmountMinor" INTEGER NOT NULL,
                    "CurrencyCode" TEXT NOT NULL,
                    "BillingIntervalMonths" INTEGER NOT NULL,
                    "NextBillingDate" TEXT NOT NULL,
                    "ReminderEnabled" INTEGER NOT NULL,
                    "CreatedAtUtc" TEXT NOT NULL,
                    "UpdatedAtUtc" TEXT NOT NULL,
                    "DeletedAtUtc" TEXT NULL,
                    "Version" TEXT NOT NULL
                );
                """, cancellationToken).ConfigureAwait(false);
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "Incomes" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_Incomes" PRIMARY KEY,
                    "AmountMinor" INTEGER NOT NULL,
                    "CurrencyCode" TEXT NOT NULL,
                    "Description" TEXT NOT NULL,
                    "OccurredOn" TEXT NOT NULL,
                    "RecurringIncomeId" TEXT NULL,
                    "CreatedAtUtc" TEXT NOT NULL,
                    "UpdatedAtUtc" TEXT NOT NULL,
                    "DeletedAtUtc" TEXT NULL,
                    "Version" TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS "RecurringIncomes" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_RecurringIncomes" PRIMARY KEY,
                    "Name" TEXT NOT NULL,
                    "AmountMinor" INTEGER NOT NULL,
                    "CurrencyCode" TEXT NOT NULL,
                    "IntervalMonths" INTEGER NOT NULL,
                    "NextOccurrenceDate" TEXT NOT NULL,
                    "CreatedAtUtc" TEXT NOT NULL,
                    "UpdatedAtUtc" TEXT NOT NULL,
                    "DeletedAtUtc" TEXT NULL,
                    "Version" TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS "SavingsGoals" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_SavingsGoals" PRIMARY KEY,
                    "Name" TEXT NOT NULL,
                    "TargetAmountMinor" INTEGER NOT NULL,
                    "SavedAmountMinor" INTEGER NOT NULL,
                    "TargetDate" TEXT NULL,
                    "CreatedAtUtc" TEXT NOT NULL,
                    "UpdatedAtUtc" TEXT NOT NULL,
                    "DeletedAtUtc" TEXT NULL,
                    "Version" TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS "FinancialProfiles" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_FinancialProfiles" PRIMARY KEY,
                    "InitialBalanceMinor" INTEGER NOT NULL,
                    "UpdatedAtUtc" TEXT NOT NULL
                );
                INSERT OR IGNORE INTO "FinancialProfiles" ("Id", "InitialBalanceMinor", "UpdatedAtUtc")
                VALUES (1, 0, CURRENT_TIMESTAMP);
                """, cancellationToken).ConfigureAwait(false);
            try
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Categories\" ADD COLUMN \"BudgetMinor\" INTEGER NULL;", cancellationToken).ConfigureAwait(false);
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 1 && ex.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase))
            {
                // La colonne existe déjà.
            }
            if (!await db.Categories.AnyAsync(cancellationToken).ConfigureAwait(false))
            {
                db.Categories.AddRange(DefaultCategories.CreateInitialSet());
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var db = CreateContext();
        return await db.Categories.AsNoTracking().Where(x => x.DeletedAtUtc == null).OrderBy(x => x.Name).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Expense>> GetExpensesAsync(ExpenseFilter? filter = null, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var db = CreateContext();
        var query = db.Expenses.AsNoTracking().Include(x => x.Category).Where(x => x.DeletedAtUtc == null);
        if (!string.IsNullOrWhiteSpace(filter?.Search)) query = query.Where(x => x.Description.Contains(filter.Search));
        if (filter?.CategoryId is Guid categoryId) query = query.Where(x => x.CategoryId == categoryId);
        if (filter?.From is DateTime from) query = query.Where(x => x.OccurredOn >= from.Date);
        if (filter?.To is DateTime to) query = query.Where(x => x.OccurredOn <= to.Date);
        return await query.OrderByDescending(x => x.OccurredOn).ThenByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddExpenseAsync(long amountMinor, string description, DateTime occurredOn, Guid categoryId, string currencyCode = "EUR", CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var db = CreateContext();
        db.Expenses.Add(new Expense { AmountMinor = amountMinor, CurrencyCode = currencyCode, Description = description.Trim(), OccurredOn = occurredOn.Date, CategoryId = categoryId });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateExpenseAsync(Guid id, long amountMinor, string description, DateTime occurredOn, Guid categoryId, string currencyCode = "EUR", CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var db = CreateContext();
        var expense = await db.Expenses.FindAsync([id], cancellationToken).ConfigureAwait(false);
        if (expense is null) return;
        expense.AmountMinor = amountMinor;
        expense.Description = description.Trim();
        expense.OccurredOn = occurredOn.Date;
        expense.CategoryId = categoryId;
        expense.CurrencyCode = currencyCode;
        expense.UpdatedAtUtc = DateTime.UtcNow;
        expense.Version = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SoftDeleteExpenseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var db = CreateContext();
        var expense = await db.Expenses.FindAsync([id], cancellationToken).ConfigureAwait(false);
        if (expense is null) return;
        expense.DeletedAtUtc = expense.UpdatedAtUtc = DateTime.UtcNow;
        expense.Version = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RestoreExpenseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var db = CreateContext();
        var expense = await db.Expenses.FindAsync([id], cancellationToken).ConfigureAwait(false);
        if (expense is null) return;
        expense.DeletedAtUtc = null;
        expense.UpdatedAtUtc = DateTime.UtcNow;
        expense.Version = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Income>> GetIncomesAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var db = CreateContext();
        return await db.Incomes.AsNoTracking().Where(x => x.DeletedAtUtc == null)
            .OrderByDescending(x => x.OccurredOn).ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddIncomeAsync(long amountMinor, string description, DateTime occurredOn, string currencyCode = "EUR", CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var db = CreateContext();
        db.Incomes.Add(new Income { AmountMinor = amountMinor, Description = description.Trim(), OccurredOn = occurredOn.Date, CurrencyCode = currencyCode });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateIncomeAsync(Guid id, long amountMinor, string description, DateTime occurredOn, string currencyCode = "EUR", CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var db = CreateContext();
        var income = await db.Incomes.FindAsync([id], cancellationToken).ConfigureAwait(false);
        if (income is null || income.DeletedAtUtc is not null) return;
        income.AmountMinor = amountMinor;
        income.Description = description.Trim();
        income.OccurredOn = occurredOn.Date;
        income.CurrencyCode = currencyCode;
        income.UpdatedAtUtc = DateTime.UtcNow;
        income.Version = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SoftDeleteIncomeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var db = CreateContext();
        var income = await db.Incomes.FindAsync([id], cancellationToken).ConfigureAwait(false);
        if (income is null) return;
        income.DeletedAtUtc = income.UpdatedAtUtc = DateTime.UtcNow;
        income.Version = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RecurringIncome>> GetRecurringIncomesAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var db = CreateContext();
        return await db.RecurringIncomes.AsNoTracking().Where(x => x.DeletedAtUtc == null)
            .OrderBy(x => x.NextOccurrenceDate).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddRecurringIncomeAsync(string name, long amountMinor, int intervalMonths, DateTime nextOccurrenceDate, string currencyCode = "EUR", CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var db = CreateContext();
        db.RecurringIncomes.Add(new RecurringIncome { Name = name.Trim(), AmountMinor = amountMinor, CurrencyCode = currencyCode, IntervalMonths = Math.Max(1, intervalMonths), NextOccurrenceDate = nextOccurrenceDate.Date });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateRecurringIncomeAsync(Guid id, string name, long amountMinor, int intervalMonths, DateTime nextOccurrenceDate, string currencyCode = "EUR", CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var db = CreateContext();
        var recurring = await db.RecurringIncomes.FindAsync([id], cancellationToken).ConfigureAwait(false);
        if (recurring is null || recurring.DeletedAtUtc is not null) return;
        recurring.Name = name.Trim();
        recurring.AmountMinor = amountMinor;
        recurring.CurrencyCode = currencyCode;
        recurring.IntervalMonths = Math.Max(1, intervalMonths);
        recurring.NextOccurrenceDate = nextOccurrenceDate.Date;
        recurring.UpdatedAtUtc = DateTime.UtcNow;
        recurring.Version = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SoftDeleteRecurringIncomeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var db = CreateContext();
        var recurring = await db.RecurringIncomes.FindAsync([id], cancellationToken).ConfigureAwait(false);
        if (recurring is null) return;
        recurring.DeletedAtUtc = recurring.UpdatedAtUtc = DateTime.UtcNow;
        recurring.Version = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<long> GetInitialBalanceAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var db = CreateContext();
        return (await db.FinancialProfiles.AsNoTracking().SingleAsync(x => x.Id == FinancialProfile.SingletonId, cancellationToken).ConfigureAwait(false)).InitialBalanceMinor;
    }

    public async Task SetInitialBalanceAsync(long amountMinor, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var db = CreateContext();
        var profile = await db.FinancialProfiles.SingleAsync(x => x.Id == FinancialProfile.SingletonId, cancellationToken).ConfigureAwait(false);
        profile.InitialBalanceMinor = amountMinor;
        profile.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddCategoryAsync(string name, string color, long? budgetMinor = null, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var db = CreateContext();
        db.Categories.Add(new Category { Name = name.Trim(), Color = color, BudgetMinor = budgetMinor });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateCategoryAsync(Guid id, string name, string color, long? budgetMinor, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var db = CreateContext();
        var category = await db.Categories.FindAsync([id], cancellationToken).ConfigureAwait(false);
        if (category is null) return;
        category.Name = name.Trim();
        category.Color = color;
        category.BudgetMinor = budgetMinor;
        category.UpdatedAtUtc = DateTime.UtcNow;
        category.Version = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SoftDeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var db = CreateContext();
        if (await db.Expenses.AnyAsync(x => x.CategoryId == id && x.DeletedAtUtc == null, cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Cette catégorie est utilisée par une dépense.");
        var category = await db.Categories.FindAsync([id], cancellationToken).ConfigureAwait(false);
        if (category is null) return;
        category.DeletedAtUtc = category.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Subscription>> GetSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var db = CreateContext();
        return await db.Subscriptions.AsNoTracking().Where(x => x.DeletedAtUtc == null).OrderBy(x => x.NextBillingDate).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddSubscriptionAsync(string name, long amountMinor, int billingIntervalMonths, DateTime nextBillingDate, string currencyCode = "EUR", CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var db = CreateContext();
        db.Subscriptions.Add(new Subscription { Name = name.Trim(), AmountMinor = amountMinor, CurrencyCode = currencyCode, BillingIntervalMonths = billingIntervalMonths, NextBillingDate = nextBillingDate.Date });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateSubscriptionAsync(Guid id, string name, long amountMinor, int billingIntervalMonths, DateTime nextBillingDate, string currencyCode = "EUR", CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var db = CreateContext();
        var subscription = await db.Subscriptions.FindAsync([id], cancellationToken).ConfigureAwait(false);
        if (subscription is null || subscription.DeletedAtUtc is not null) return;
        subscription.Name = name.Trim();
        subscription.AmountMinor = amountMinor;
        subscription.CurrencyCode = currencyCode;
        subscription.BillingIntervalMonths = Math.Max(1, billingIntervalMonths);
        subscription.NextBillingDate = nextBillingDate.Date;
        subscription.UpdatedAtUtc = DateTime.UtcNow;
        subscription.Version = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SoftDeleteSubscriptionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var db = CreateContext();
        var subscription = await db.Subscriptions.FindAsync([id], cancellationToken).ConfigureAwait(false);
        if (subscription is null) return;
        subscription.DeletedAtUtc = subscription.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ApplyDueOperationsAsync(DueOperationsPlan plan, CancellationToken cancellationToken = default)
    {
        if (plan.IsEmpty) return;
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var db = CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        if (plan.Expenses.Count > 0)
        {
            var category = await db.Categories.FirstOrDefaultAsync(x => x.DeletedAtUtc == null && x.Name == DefaultCategories.SubscriptionsName, cancellationToken).ConfigureAwait(false);
            if (category is null)
            {
                category = DefaultCategories.CreateSubscriptions();
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
            subscription.UpdatedAtUtc = DateTime.UtcNow;
            subscription.Version = Guid.NewGuid();
        }
        foreach (var advance in plan.RecurringIncomeAdvances)
        {
            var recurring = await db.RecurringIncomes.FindAsync([advance.Id], cancellationToken).ConfigureAwait(false);
            if (recurring is null) continue;
            recurring.NextOccurrenceDate = advance.NextDate.Date;
            recurring.UpdatedAtUtc = DateTime.UtcNow;
            recurring.Version = Guid.NewGuid();
        }
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SavingsGoal>> GetSavingsGoalsAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var db = CreateContext();
        return await db.SavingsGoals.AsNoTracking().Where(x => x.DeletedAtUtc == null)
            .OrderBy(x => x.TargetDate).ThenBy(x => x.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddSavingsGoalAsync(string name, long targetAmountMinor, long savedAmountMinor, DateTime? targetDate, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var db = CreateContext();
        db.SavingsGoals.Add(new SavingsGoal { Name = name.Trim(), TargetAmountMinor = targetAmountMinor, SavedAmountMinor = savedAmountMinor, TargetDate = targetDate?.Date });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateSavingsGoalAsync(Guid id, string name, long targetAmountMinor, long savedAmountMinor, DateTime? targetDate, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var db = CreateContext();
        var goal = await db.SavingsGoals.FindAsync([id], cancellationToken).ConfigureAwait(false);
        if (goal is null || goal.DeletedAtUtc is not null) return;
        goal.Name = name.Trim();
        goal.TargetAmountMinor = targetAmountMinor;
        goal.SavedAmountMinor = savedAmountMinor;
        goal.TargetDate = targetDate?.Date;
        goal.UpdatedAtUtc = DateTime.UtcNow;
        goal.Version = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SoftDeleteSavingsGoalAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var db = CreateContext();
        var goal = await db.SavingsGoals.FindAsync([id], cancellationToken).ConfigureAwait(false);
        if (goal is null) return;
        goal.DeletedAtUtc = goal.UpdatedAtUtc = DateTime.UtcNow;
        goal.Version = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<FinanceBackupSnapshot> CreateBackupSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var db = CreateContext();
        var categories = await db.Categories.AsNoTracking().Where(x => x.DeletedAtUtc == null).Select(x => new CategoryBackup(x.Id, x.Name, x.Color, x.BudgetMinor, x.CreatedAtUtc)).ToListAsync(cancellationToken).ConfigureAwait(false);
        var expenses = await db.Expenses.AsNoTracking().Where(x => x.DeletedAtUtc == null).Select(x => new ExpenseBackup(x.Id, x.AmountMinor, x.CurrencyCode, x.Description, x.OccurredOn, x.CategoryId, x.CreatedAtUtc)).ToListAsync(cancellationToken).ConfigureAwait(false);
        var subscriptions = await db.Subscriptions.AsNoTracking().Where(x => x.DeletedAtUtc == null).Select(x => new SubscriptionBackup(x.Id, x.Name, x.AmountMinor, x.CurrencyCode, x.BillingIntervalMonths, x.NextBillingDate, x.ReminderEnabled, x.CreatedAtUtc)).ToListAsync(cancellationToken).ConfigureAwait(false);
        var incomes = await db.Incomes.AsNoTracking().Where(x => x.DeletedAtUtc == null).Select(x => new IncomeBackup(x.Id, x.AmountMinor, x.CurrencyCode, x.Description, x.OccurredOn, x.RecurringIncomeId, x.CreatedAtUtc)).ToListAsync(cancellationToken).ConfigureAwait(false);
        var recurringIncomes = await db.RecurringIncomes.AsNoTracking().Where(x => x.DeletedAtUtc == null).Select(x => new RecurringIncomeBackup(x.Id, x.Name, x.AmountMinor, x.CurrencyCode, x.IntervalMonths, x.NextOccurrenceDate, x.CreatedAtUtc)).ToListAsync(cancellationToken).ConfigureAwait(false);
        var savingsGoals = await db.SavingsGoals.AsNoTracking().Where(x => x.DeletedAtUtc == null).Select(x => new SavingsGoalBackup(x.Id, x.Name, x.TargetAmountMinor, x.SavedAmountMinor, x.TargetDate, x.CreatedAtUtc)).ToListAsync(cancellationToken).ConfigureAwait(false);
        var initialBalance = await db.FinancialProfiles.AsNoTracking().Where(x => x.Id == FinancialProfile.SingletonId).Select(x => x.InitialBalanceMinor).SingleAsync(cancellationToken).ConfigureAwait(false);
        return new FinanceBackupSnapshot(2, DateTime.UtcNow, categories, expenses, subscriptions, incomes, recurringIncomes, savingsGoals, initialBalance);
    }

    public async Task RestoreBackupSnapshotAsync(FinanceBackupSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        if (snapshot.FormatVersion is not (1 or 2)) throw new InvalidOperationException("Format de sauvegarde non pris en charge.");
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var db = CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        db.Expenses.RemoveRange(await db.Expenses.ToListAsync(cancellationToken).ConfigureAwait(false));
        db.Subscriptions.RemoveRange(await db.Subscriptions.ToListAsync(cancellationToken).ConfigureAwait(false));
        db.Incomes.RemoveRange(await db.Incomes.ToListAsync(cancellationToken).ConfigureAwait(false));
        db.RecurringIncomes.RemoveRange(await db.RecurringIncomes.ToListAsync(cancellationToken).ConfigureAwait(false));
        db.SavingsGoals.RemoveRange(await db.SavingsGoals.ToListAsync(cancellationToken).ConfigureAwait(false));
        db.Categories.RemoveRange(await db.Categories.ToListAsync(cancellationToken).ConfigureAwait(false));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        db.Categories.AddRange(snapshot.Categories.Select(x => new Category { Id = x.Id, Name = x.Name, Color = x.Color, BudgetMinor = x.BudgetMinor, CreatedAtUtc = x.CreatedAtUtc }));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        db.Expenses.AddRange(snapshot.Expenses.Select(x => new Expense { Id = x.Id, AmountMinor = x.AmountMinor, CurrencyCode = x.CurrencyCode, Description = x.Description, OccurredOn = x.OccurredOn, CategoryId = x.CategoryId, CreatedAtUtc = x.CreatedAtUtc }));
        db.Subscriptions.AddRange(snapshot.Subscriptions.Select(x => new Subscription { Id = x.Id, Name = x.Name, AmountMinor = x.AmountMinor, CurrencyCode = x.CurrencyCode, BillingIntervalMonths = x.BillingIntervalMonths, NextBillingDate = x.NextBillingDate, ReminderEnabled = x.ReminderEnabled, CreatedAtUtc = x.CreatedAtUtc }));
        db.Incomes.AddRange((snapshot.Incomes ?? []).Select(x => new Income { Id = x.Id, AmountMinor = x.AmountMinor, CurrencyCode = x.CurrencyCode, Description = x.Description, OccurredOn = x.OccurredOn, RecurringIncomeId = x.RecurringIncomeId, CreatedAtUtc = x.CreatedAtUtc }));
        db.RecurringIncomes.AddRange((snapshot.RecurringIncomes ?? []).Select(x => new RecurringIncome { Id = x.Id, Name = x.Name, AmountMinor = x.AmountMinor, CurrencyCode = x.CurrencyCode, IntervalMonths = x.IntervalMonths, NextOccurrenceDate = x.NextOccurrenceDate, CreatedAtUtc = x.CreatedAtUtc }));
        db.SavingsGoals.AddRange((snapshot.SavingsGoals ?? []).Select(x => new SavingsGoal { Id = x.Id, Name = x.Name, TargetAmountMinor = x.TargetAmountMinor, SavedAmountMinor = x.SavedAmountMinor, TargetDate = x.TargetDate, CreatedAtUtc = x.CreatedAtUtc }));
        var profile = await db.FinancialProfiles.SingleAsync(x => x.Id == FinancialProfile.SingletonId, cancellationToken).ConfigureAwait(false);
        profile.InitialBalanceMinor = snapshot.FormatVersion >= 2 ? snapshot.InitialBalanceMinor : 0;
        profile.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAllDataAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var db = CreateContext();
        db.Expenses.RemoveRange(await db.Expenses.ToListAsync(cancellationToken).ConfigureAwait(false));
        db.Subscriptions.RemoveRange(await db.Subscriptions.ToListAsync(cancellationToken).ConfigureAwait(false));
        db.Incomes.RemoveRange(await db.Incomes.ToListAsync(cancellationToken).ConfigureAwait(false));
        db.RecurringIncomes.RemoveRange(await db.RecurringIncomes.ToListAsync(cancellationToken).ConfigureAwait(false));
        db.SavingsGoals.RemoveRange(await db.SavingsGoals.ToListAsync(cancellationToken).ConfigureAwait(false));
        db.Categories.RemoveRange(await db.Categories.ToListAsync(cancellationToken).ConfigureAwait(false));
        var profile = await db.FinancialProfiles.SingleAsync(x => x.Id == FinancialProfile.SingletonId, cancellationToken).ConfigureAwait(false);
        profile.InitialBalanceMinor = 0;
        profile.UpdatedAtUtc = DateTime.UtcNow;
        db.Categories.AddRange(DefaultCategories.CreateInitialSet());
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private FinanceDbContext CreateContext()
    {
        if (_connectionString is null) throw new InvalidOperationException("La base n'est pas initialisée.");
        var options = new DbContextOptionsBuilder<FinanceDbContext>().UseSqlite(_connectionString).Options;
        return new FinanceDbContext(options);
    }
}
