namespace Finance.Android17.Application;

// Contenu d'une sauvegarde (.mfbackup une fois sérialisé en JSON puis chiffré). Les noms des propriétés font partie
// du format de fichier : ne pas les renommer. Les données ajoutées après la version 1 sont facultatives.

public sealed record CategoryBackup(Guid Id, string Name, string Color, long? BudgetMinor, DateTime CreatedAtUtc);
public sealed record ExpenseBackup(Guid Id, long AmountMinor, string CurrencyCode, string Description, DateTime OccurredOn, Guid CategoryId, DateTime CreatedAtUtc);
public sealed record SubscriptionBackup(Guid Id, string Name, long AmountMinor, string CurrencyCode, int BillingIntervalMonths, DateTime NextBillingDate, bool ReminderEnabled, DateTime CreatedAtUtc, int AnchorDay = 0);
public sealed record IncomeBackup(Guid Id, long AmountMinor, string CurrencyCode, string Description, DateTime OccurredOn, Guid? RecurringIncomeId, DateTime CreatedAtUtc);
public sealed record RecurringIncomeBackup(Guid Id, string Name, long AmountMinor, string CurrencyCode, int IntervalMonths, DateTime NextOccurrenceDate, DateTime CreatedAtUtc, int AnchorDay = 0);
public sealed record SavingsGoalBackup(Guid Id, string Name, long TargetAmountMinor, long SavedAmountMinor, DateTime? TargetDate, DateTime CreatedAtUtc);

/// <summary>
/// Réglages utiles de l'application, sauvegardés avec les données. Sont exclus ce qui dépend de l'appareil ou de son état :
/// biométrie, alertes déjà consultées ou envoyées, dernier contrôle quotidien, dernière catégorie utilisée.
/// </summary>
public sealed record SettingsBackup(
    bool HideAmounts,
    int AutoLockMinutes,
    string Theme,
    bool AutoCreateSubscriptionExpenses,
    bool AutoCreateRecurringIncomes,
    bool SubscriptionRemindersEnabled,
    int SubscriptionReminderDays,
    long MonthlyBudgetMinor,
    string CurrencyCode,
    string? DefaultCategoryId,
    bool RememberLastCategory,
    string AfterSaveBehavior,
    bool BackgroundAlertsEnabled);

public sealed record FinanceBackupSnapshot(
    int FormatVersion,
    DateTime CreatedAtUtc,
    IReadOnlyList<CategoryBackup> Categories,
    IReadOnlyList<ExpenseBackup> Expenses,
    IReadOnlyList<SubscriptionBackup> Subscriptions,
    IReadOnlyList<IncomeBackup>? Incomes = null,
    IReadOnlyList<RecurringIncomeBackup>? RecurringIncomes = null,
    IReadOnlyList<SavingsGoalBackup>? SavingsGoals = null,
    long InitialBalanceMinor = 0,
    SettingsBackup? Settings = null);
