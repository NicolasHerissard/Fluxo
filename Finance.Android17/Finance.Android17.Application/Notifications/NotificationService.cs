namespace Finance.Android17.Application;

/// <summary>Les alertes du moment et, parmi elles, celles que l'utilisateur n'a pas encore vues.</summary>
public sealed record NotificationFeed(IReadOnlyList<AppNotification> Items, IReadOnlySet<string> UnreadKeys)
{
    public bool HasUnread => UnreadKeys.Count > 0;
}

/// <summary>Charge les alertes (<see cref="NotificationBuilder"/>) et retient lesquelles ont été lues, dans les réglages.</summary>
public sealed class NotificationService(FinanceDataLoader loader, ISettingsService settings, TimeProvider clock)
{
    private const char Separator = '\n';

    public async Task<NotificationFeed> LoadAsync(CancellationToken cancellationToken = default)
    {
        var data = await loader.LoadAsync(cancellationToken).ConfigureAwait(false);
        var items = NotificationBuilder.Build(data, clock.GetLocalNow().Date, settings.MonthlyBudgetMinor);
        var seen = settings.SeenNotifications.Split(Separator, StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        return new NotificationFeed(items, items.Select(x => x.Key).Where(key => !seen.Contains(key)).ToHashSet(StringComparer.Ordinal));
    }

    /// <summary>
    /// Marque les alertes données comme lues. Seules les alertes actuelles sont retenues : celles d'un mois ou
    /// d'une échéance passés disparaissent d'elles-mêmes au lieu de s'accumuler dans les réglages.
    /// </summary>
    public void MarkRead(IEnumerable<AppNotification> items) =>
        settings.SeenNotifications = string.Join(Separator, items.Select(x => x.Key.Replace(Separator, ' ')));
}
