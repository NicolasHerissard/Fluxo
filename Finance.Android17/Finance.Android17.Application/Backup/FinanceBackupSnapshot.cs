namespace Finance.Android17.Application;

// Contenu d'une sauvegarde (.mfbackup une fois sérialisé en JSON puis chiffré). Les noms des propriétés font partie
// du format de fichier : ne pas les renommer. Les données ajoutées après la version 1 sont facultatives.

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
