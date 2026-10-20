using Finance.Android17.Domain;

namespace Finance.Android17.Application;

public interface ISecretStore
{
    Task<string> GetOrCreateDatabaseKeyAsync(CancellationToken cancellationToken = default);
}

public interface IAppLockService
{
    Task<bool> HasPinAsync();
    Task SetPinAsync(string pin, CancellationToken cancellationToken = default);
    Task<bool> VerifyPinAsync(string pin, CancellationToken cancellationToken = default);
}

public interface IBiometricService
{
    bool IsAvailable { get; }
    Task<bool> AuthenticateAsync(string reason, CancellationToken cancellationToken = default);
}

public interface ISettingsService
{
    bool HideAmounts { get; set; }
    bool BiometricsEnabled { get; set; }
    int AutoLockMinutes { get; set; }
    string Theme { get; set; }
    bool AutoCreateSubscriptionExpenses { get; set; }
    bool AutoCreateRecurringIncomes { get; set; }
    bool SubscriptionRemindersEnabled { get; set; }
    int SubscriptionReminderDays { get; set; }
    long MonthlyBudgetMinor { get; set; }
    string CurrencyCode { get; set; }
    string DefaultCategoryId { get; set; }
    string LastCategoryId { get; set; }
    bool RememberLastCategory { get; set; }
    string AfterSaveBehavior { get; set; }
}

public interface IExportService
{
    /// <summary>Exporte toutes les dépenses actives en CSV et ouvre la feuille de partage.</summary>
    Task ExportExpensesCsvAsync(CancellationToken cancellationToken = default);
}

public sealed record OcrTextResult(string Text);

public interface IOcrService
{
    Task<OcrTextResult?> RecognizeAsync(string imagePath, CancellationToken cancellationToken = default);
}

public interface IDataBackupService
{
    Task ExportEncryptedAsync(string password, CancellationToken cancellationToken = default);
    Task<bool> ImportEncryptedAsync(string password, CancellationToken cancellationToken = default);
}

public interface IReminderService
{
    Task ShowSubscriptionReminderAsync(IReadOnlyList<Subscription> subscriptions, int reminderDays, CancellationToken cancellationToken = default);
}

public sealed record ExpenseFilter(string? Search = null, Guid? CategoryId = null, DateTime? From = null, DateTime? To = null);

public interface IFinanceStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Expense>> GetExpensesAsync(ExpenseFilter? filter = null, CancellationToken cancellationToken = default);
    Task AddExpenseAsync(long amountMinor, string description, DateTime occurredOn, Guid categoryId, string currencyCode = "EUR", CancellationToken cancellationToken = default);
    Task UpdateExpenseAsync(Guid id, long amountMinor, string description, DateTime occurredOn, Guid categoryId, string currencyCode = "EUR", CancellationToken cancellationToken = default);
    Task SoftDeleteExpenseAsync(Guid id, CancellationToken cancellationToken = default);
    Task RestoreExpenseAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Income>> GetIncomesAsync(CancellationToken cancellationToken = default);
    Task AddIncomeAsync(long amountMinor, string description, DateTime occurredOn, string currencyCode = "EUR", CancellationToken cancellationToken = default);
    Task UpdateIncomeAsync(Guid id, long amountMinor, string description, DateTime occurredOn, string currencyCode = "EUR", CancellationToken cancellationToken = default);
    Task SoftDeleteIncomeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecurringIncome>> GetRecurringIncomesAsync(CancellationToken cancellationToken = default);
    Task AddRecurringIncomeAsync(string name, long amountMinor, int intervalMonths, DateTime nextOccurrenceDate, string currencyCode = "EUR", CancellationToken cancellationToken = default);
    Task UpdateRecurringIncomeAsync(Guid id, string name, long amountMinor, int intervalMonths, DateTime nextOccurrenceDate, string currencyCode = "EUR", CancellationToken cancellationToken = default);
    Task SoftDeleteRecurringIncomeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> ProcessDueRecurringIncomesAsync(DateTime today, string currencyCode, CancellationToken cancellationToken = default);
    Task<long> GetInitialBalanceAsync(CancellationToken cancellationToken = default);
    Task SetInitialBalanceAsync(long amountMinor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SavingsGoal>> GetSavingsGoalsAsync(CancellationToken cancellationToken = default);
    Task AddSavingsGoalAsync(string name, long targetAmountMinor, long savedAmountMinor, DateTime? targetDate, CancellationToken cancellationToken = default);
    Task UpdateSavingsGoalAsync(Guid id, string name, long targetAmountMinor, long savedAmountMinor, DateTime? targetDate, CancellationToken cancellationToken = default);
    Task SoftDeleteSavingsGoalAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddCategoryAsync(string name, string color, long? budgetMinor = null, CancellationToken cancellationToken = default);
    Task UpdateCategoryAsync(Guid id, string name, string color, long? budgetMinor, CancellationToken cancellationToken = default);
    Task SoftDeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Subscription>> GetSubscriptionsAsync(CancellationToken cancellationToken = default);
    Task AddSubscriptionAsync(string name, long amountMinor, int billingIntervalMonths, DateTime nextBillingDate, string currencyCode = "EUR", CancellationToken cancellationToken = default);
    Task UpdateSubscriptionAsync(Guid id, string name, long amountMinor, int billingIntervalMonths, DateTime nextBillingDate, string currencyCode = "EUR", CancellationToken cancellationToken = default);
    Task SoftDeleteSubscriptionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> ProcessDueSubscriptionsAsync(DateTime today, string currencyCode, CancellationToken cancellationToken = default);
    Task<FinanceBackupSnapshot> CreateBackupSnapshotAsync(CancellationToken cancellationToken = default);
    Task RestoreBackupSnapshotAsync(FinanceBackupSnapshot snapshot, CancellationToken cancellationToken = default);
    Task DeleteAllDataAsync(CancellationToken cancellationToken = default);
}

public sealed record CategoryBackup(Guid Id, string Name, string Color, long? BudgetMinor, DateTime CreatedAtUtc);
public sealed record ExpenseBackup(Guid Id, long AmountMinor, string CurrencyCode, string Description, DateTime OccurredOn, Guid CategoryId, DateTime CreatedAtUtc);
public sealed record SubscriptionBackup(Guid Id, string Name, long AmountMinor, string CurrencyCode, int BillingIntervalMonths, DateTime NextBillingDate, bool ReminderEnabled, DateTime CreatedAtUtc);
public sealed record IncomeBackup(Guid Id, long AmountMinor, string CurrencyCode, string Description, DateTime OccurredOn, Guid? RecurringIncomeId, DateTime CreatedAtUtc);
public sealed record RecurringIncomeBackup(Guid Id, string Name, long AmountMinor, string CurrencyCode, int IntervalMonths, DateTime NextOccurrenceDate, DateTime CreatedAtUtc);
public sealed record SavingsGoalBackup(Guid Id, string Name, long TargetAmountMinor, long SavedAmountMinor, DateTime? TargetDate, DateTime CreatedAtUtc);
public sealed record FinanceBackupSnapshot(
    int FormatVersion,
    DateTime CreatedAtUtc,
    IReadOnlyList<CategoryBackup> Categories,
    IReadOnlyList<ExpenseBackup> Expenses,
    IReadOnlyList<SubscriptionBackup> Subscriptions,
    IReadOnlyList<IncomeBackup>? Incomes = null,
    IReadOnlyList<RecurringIncomeBackup>? RecurringIncomes = null,
    IReadOnlyList<SavingsGoalBackup>? SavingsGoals = null,
    long InitialBalanceMinor = 0);

public sealed record DashboardSummary(long MonthTotalMinor, int ExpenseCount, string TopCategory);
public sealed record FinancialSummary(
    long ExpenseTotalMinor,
    long IncomeTotalMinor,
    long NetTotalMinor,
    long CurrentBalanceMinor,
    long ForecastBalanceMinor,
    double SavingsRate);
