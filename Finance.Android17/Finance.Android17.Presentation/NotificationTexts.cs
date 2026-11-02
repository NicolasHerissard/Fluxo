using Finance.Android17.Application;

namespace Finance.Android17.Presentation;

/// <summary>Texte d'une alerte : le titre et le message, plus l'icône du centre de notifications.</summary>
public sealed record NotificationMessage(string Title, string Text, string Icon);

/// <summary>
/// Compose le texte d'une <see cref="AppNotification"/>. Partagé par le centre de notifications (<see cref="NotificationsViewModel"/>)
/// et par les notifications du système envoyées en arrière-plan, pour que les deux disent la même chose.
/// </summary>
public static class NotificationTexts
{
    /// <summary>Formate un montant en centimes dans la devise donnée, ou le masque quand les montants sont cachés.</summary>
    public static Func<long, string> Amounts(bool hideAmounts, string currencyCode) =>
        minor => hideAmounts ? MoneyFormatter.Hidden : MoneyFormatter.Money(minor, currencyCode);

    public static NotificationMessage Compose(AppNotification item, Func<long, string> amount)
    {
        var percent = item.LimitMinor > 0 ? MoneyFormatter.Percent((double)item.AmountMinor / item.LimitMinor) : string.Empty;
        var exceeded = item.Severity == NotificationSeverity.Critical;

        return item.Topic switch
        {
            NotificationTopic.SubscriptionDue => new(DueTitle(item.DaysAway), $"{item.Subject} · {amount(item.AmountMinor)}", "calendar"),
            NotificationTopic.MonthlyBudget => new(
                exceeded ? "Budget mensuel dépassé" : "Budget mensuel bientôt atteint",
                $"{amount(item.AmountMinor)} dépensés sur {amount(item.LimitMinor)} ({percent}).",
                "wallet"),
            NotificationTopic.CategoryBudget => new(
                exceeded ? $"Budget « {item.Subject} » dépassé" : $"Budget « {item.Subject} » bientôt atteint",
                $"{amount(item.AmountMinor)} dépensés sur {amount(item.LimitMinor)} ({percent}).",
                "wallet"),
            _ => new(
                "Découvert probable en fin de mois",
                $"Solde estimé au dernier jour du mois : {amount(item.AmountMinor)}.",
                "trending-down")
        };
    }

    private static string DueTitle(int days) => days switch
    {
        0 => "Prélèvement aujourd’hui",
        1 => "Prélèvement demain",
        _ => $"Prélèvement dans {days} jours"
    };
}
