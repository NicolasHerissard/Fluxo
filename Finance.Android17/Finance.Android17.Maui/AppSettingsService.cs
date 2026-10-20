using Finance.Android17.Application;

namespace Finance.Android17.Maui;

public sealed class AppSettingsService : ISettingsService
{
    public bool HideAmounts { get => Preferences.Default.Get(nameof(HideAmounts), false); set => Preferences.Default.Set(nameof(HideAmounts), value); }
    public bool BiometricsEnabled { get => Preferences.Default.Get(nameof(BiometricsEnabled), false); set => Preferences.Default.Set(nameof(BiometricsEnabled), value); }
    public int AutoLockMinutes { get => Preferences.Default.Get(nameof(AutoLockMinutes), 5); set => Preferences.Default.Set(nameof(AutoLockMinutes), value); }
    public bool AutoCreateSubscriptionExpenses { get => Preferences.Default.Get(nameof(AutoCreateSubscriptionExpenses), false); set => Preferences.Default.Set(nameof(AutoCreateSubscriptionExpenses), value); }
    public bool AutoCreateRecurringIncomes { get => Preferences.Default.Get(nameof(AutoCreateRecurringIncomes), true); set => Preferences.Default.Set(nameof(AutoCreateRecurringIncomes), value); }
    public bool SubscriptionRemindersEnabled { get => Preferences.Default.Get(nameof(SubscriptionRemindersEnabled), true); set => Preferences.Default.Set(nameof(SubscriptionRemindersEnabled), value); }
    public int SubscriptionReminderDays { get => Preferences.Default.Get(nameof(SubscriptionReminderDays), 3); set => Preferences.Default.Set(nameof(SubscriptionReminderDays), value); }
    public long MonthlyBudgetMinor { get => Preferences.Default.Get(nameof(MonthlyBudgetMinor), 0L); set => Preferences.Default.Set(nameof(MonthlyBudgetMinor), value); }
    public string CurrencyCode { get => Preferences.Default.Get(nameof(CurrencyCode), "EUR"); set => Preferences.Default.Set(nameof(CurrencyCode), value); }
    public string DefaultCategoryId { get => Preferences.Default.Get(nameof(DefaultCategoryId), string.Empty); set => Preferences.Default.Set(nameof(DefaultCategoryId), value); }
    public string LastCategoryId { get => Preferences.Default.Get(nameof(LastCategoryId), string.Empty); set => Preferences.Default.Set(nameof(LastCategoryId), value); }
    public bool RememberLastCategory { get => Preferences.Default.Get(nameof(RememberLastCategory), true); set => Preferences.Default.Set(nameof(RememberLastCategory), value); }
    public string AfterSaveBehavior { get => Preferences.Default.Get(nameof(AfterSaveBehavior), "Fermer"); set => Preferences.Default.Set(nameof(AfterSaveBehavior), value); }
    public string Theme
    {
        get => Preferences.Default.Get(nameof(Theme), "Système");
        set
        {
            Preferences.Default.Set(nameof(Theme), value);
            if (Microsoft.Maui.Controls.Application.Current is { } app)
                app.UserAppTheme = value switch { "Clair" => AppTheme.Light, "Sombre" => AppTheme.Dark, _ => AppTheme.Unspecified };
            SystemBarAppearance.Apply();
        }
    }

    public void ApplyTheme()
    {
        if (Microsoft.Maui.Controls.Application.Current is { } app)
            app.UserAppTheme = Theme switch { "Clair" => AppTheme.Light, "Sombre" => AppTheme.Dark, _ => AppTheme.Unspecified };
        SystemBarAppearance.Apply();
    }
}
