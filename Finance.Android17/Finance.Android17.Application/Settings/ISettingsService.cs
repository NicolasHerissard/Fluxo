namespace Finance.Android17.Application;

/// <summary>Préférences de l'utilisateur, conservées sur l'appareil.</summary>
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
