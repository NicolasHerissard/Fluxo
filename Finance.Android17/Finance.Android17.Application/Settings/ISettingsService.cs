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

    /// <summary>Clés (séparées par un saut de ligne) des alertes déjà consultées dans le centre de notifications.</summary>
    string SeenNotifications { get; set; }

    /// <summary>Envoyer les nouvelles alertes du centre de notifications en notification du système, chaque matin, application fermée.</summary>
    bool BackgroundAlertsEnabled { get; set; }

    /// <summary>Clés (séparées par un saut de ligne) des alertes déjà envoyées en notification du système.</summary>
    string PushedNotifications { get; set; }

    /// <summary>Jour (<c>yyyy-MM-dd</c>, heure locale) du dernier contrôle quotidien lancé ; vide tant qu'aucun n'a eu lieu.</summary>
    string LastDailyCheckDay { get; set; }
}
