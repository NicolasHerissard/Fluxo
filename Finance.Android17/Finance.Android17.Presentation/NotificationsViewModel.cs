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
        var amount = NotificationTexts.Amounts(settings.HideAmounts, settings.CurrencyCode);

        Items.Clear();
        foreach (var item in feed.Items) Items.Add(ToRow(item, feed.UnreadKeys.Contains(item.Key), amount));

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
        var text = NotificationTexts.Compose(item, amount);
        return new NotificationRow(item.Key, text.Title, text.Text, text.Icon, severity, isNew);
    }
}
