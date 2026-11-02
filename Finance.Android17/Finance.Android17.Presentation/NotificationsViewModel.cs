using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Android17.Application;

namespace Finance.Android17.Presentation;

/// <summary>Centre de notifications : budgets proches ou dépassés, découvert probable et prélèvements des sept prochains jours.</summary>
public partial class NotificationsViewModel(NotificationService notifications, ISettingsService settings) : ObservableObject
{
    private IReadOnlyList<AppNotification> _current = [];

    public ObservableCollection<NotificationRow> Items { get; } = [];
    [ObservableProperty] private bool hasUnread;
    [ObservableProperty] private bool isEmpty = true;
    [ObservableProperty] private string summary = "Aucune alerte pour le moment.";

    /// <summary>Charge les alertes du moment. Ne marque rien comme lu : voir <see cref="MarkAllRead"/>.</summary>
    [RelayCommand]
    public async Task LoadAsync()
    {
        var feed = await notifications.LoadAsync();
        _current = feed.Items;
        var hide = settings.HideAmounts;
        var currency = settings.CurrencyCode;
        string Amount(long minor) => hide ? MoneyFormatter.Hidden : MoneyFormatter.Money(minor, currency);

        Items.Clear();
        foreach (var item in feed.Items) Items.Add(ToRow(item, feed.UnreadKeys.Contains(item.Key), Amount));

        HasUnread = feed.HasUnread;
        IsEmpty = Items.Count == 0;
        Summary = Items.Count switch
        {
            0 => "Aucune alerte pour le moment.",
            1 => "1 alerte à consulter.",
            var count => $"{count} alertes à consulter."
        };
    }

    /// <summary>L'utilisateur a vu la liste : le point de la cloche disparaît jusqu'à la prochaine nouvelle alerte.</summary>
    [RelayCommand]
    public void MarkAllRead()
    {
        notifications.MarkRead(_current);
        HasUnread = false;
        for (var i = 0; i < Items.Count; i++)
            if (Items[i].IsNew) Items[i] = Items[i] with { IsNew = false };
    }

    private static NotificationRow ToRow(AppNotification item, bool isNew, Func<long, string> amount)
    {
        var severity = item.Severity switch { NotificationSeverity.Critical => "critical", NotificationSeverity.Warning => "warning", _ => "info" };
        var percent = item.LimitMinor > 0 ? MoneyFormatter.Percent((double)item.AmountMinor / item.LimitMinor) : string.Empty;
        var exceeded = item.Severity == NotificationSeverity.Critical;

        var (title, message, icon) = item.Topic switch
        {
            NotificationTopic.SubscriptionDue => (DueTitle(item.DaysAway), $"{item.Subject} · {amount(item.AmountMinor)}", "calendar"),
            NotificationTopic.MonthlyBudget => (
                exceeded ? "Budget mensuel dépassé" : "Budget mensuel bientôt atteint",
                $"{amount(item.AmountMinor)} dépensés sur {amount(item.LimitMinor)} ({percent}).",
                "wallet"),
            NotificationTopic.CategoryBudget => (
                exceeded ? $"Budget « {item.Subject} » dépassé" : $"Budget « {item.Subject} » bientôt atteint",
                $"{amount(item.AmountMinor)} dépensés sur {amount(item.LimitMinor)} ({percent}).",
                "wallet"),
            _ => (
                "Découvert probable en fin de mois",
                $"Solde estimé au dernier jour du mois : {amount(item.AmountMinor)}.",
                "trending-down")
        };
        return new NotificationRow(item.Key, title, message, icon, severity, isNew);
    }

    private static string DueTitle(int days) => days switch
    {
        0 => "Prélèvement aujourd’hui",
        1 => "Prélèvement demain",
        _ => $"Prélèvement dans {days} jours"
    };
}
