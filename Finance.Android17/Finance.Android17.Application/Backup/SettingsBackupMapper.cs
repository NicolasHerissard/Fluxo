using Finance.Android17.Domain;

namespace Finance.Android17.Application;

/// <summary>Passage des réglages de l'appareil vers une sauvegarde et inversement.</summary>
public static class SettingsBackupMapper
{
    private const int MaxAutoLockMinutes = 24 * 60;
    private const int MaxReminderDays = 30;

    public static SettingsBackup Capture(ISettingsService settings) => new(
        settings.HideAmounts,
        settings.AutoLockMinutes,
        settings.Theme,
        settings.AutoCreateSubscriptionExpenses,
        settings.AutoCreateRecurringIncomes,
        settings.SubscriptionRemindersEnabled,
        settings.SubscriptionReminderDays,
        settings.MonthlyBudgetMinor,
        settings.CurrencyCode,
        string.IsNullOrEmpty(settings.DefaultCategoryId) ? null : settings.DefaultCategoryId,
        settings.RememberLastCategory,
        settings.AfterSaveBehavior,
        settings.BackgroundAlertsEnabled);

    /// <summary>
    /// Applique les réglages d'une sauvegarde. Une valeur hors limites est ignorée (le réglage actuel reste) plutôt que de
    /// refuser toute la restauration. La catégorie par défaut n'est reprise que si elle existe dans <paramref name="categoryIds"/>.
    /// </summary>
    public static void Apply(SettingsBackup backup, ISettingsService settings, IReadOnlySet<Guid> categoryIds)
    {
        settings.HideAmounts = backup.HideAmounts;
        if (backup.AutoLockMinutes is > 0 and <= MaxAutoLockMinutes) settings.AutoLockMinutes = backup.AutoLockMinutes;
        if (backup.Theme is not null && ThemeOptions.All.Contains(backup.Theme)) settings.Theme = backup.Theme;
        settings.AutoCreateSubscriptionExpenses = backup.AutoCreateSubscriptionExpenses;
        settings.AutoCreateRecurringIncomes = backup.AutoCreateRecurringIncomes;
        settings.SubscriptionRemindersEnabled = backup.SubscriptionRemindersEnabled;
        if (backup.SubscriptionReminderDays is > 0 and <= MaxReminderDays) settings.SubscriptionReminderDays = backup.SubscriptionReminderDays;
        if (backup.MonthlyBudgetMinor is >= 0 and <= 10_000_000_000_000) settings.MonthlyBudgetMinor = backup.MonthlyBudgetMinor;
        if (backup.CurrencyCode is not null && CurrencyCodes.Supported.Contains(backup.CurrencyCode)) settings.CurrencyCode = backup.CurrencyCode;
        settings.DefaultCategoryId = Guid.TryParse(backup.DefaultCategoryId, out var id) && categoryIds.Contains(id) ? id.ToString() : string.Empty;
        settings.RememberLastCategory = backup.RememberLastCategory;
        if (backup.AfterSaveBehavior is not null && AfterSaveOptions.All.Contains(backup.AfterSaveBehavior)) settings.AfterSaveBehavior = backup.AfterSaveBehavior;
        settings.BackgroundAlertsEnabled = backup.BackgroundAlertsEnabled;
        // La dernière catégorie utilisée peut ne plus exister après la restauration.
        if (!Guid.TryParse(settings.LastCategoryId, out var last) || !categoryIds.Contains(last)) settings.LastCategoryId = string.Empty;
    }
}
