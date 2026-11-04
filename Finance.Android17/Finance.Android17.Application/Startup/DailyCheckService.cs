namespace Finance.Android17.Application;

/// <summary>
/// Contrôle quotidien, lancé par une alarme de la plateforme sans que l'application soit ouverte : rattrape les
/// échéances, reprogramme les rappels, puis envoie en notification les nouvelles alertes du centre de notifications.
/// </summary>
public sealed class DailyCheckService(
    StartupService startup,
    NotificationService notifications,
    IAlertPublisher publisher,
    ISettingsService settings,
    TimeProvider clock)
{
    /// <summary>Heure locale du contrôle : avant celle des rappels d'échéance (<see cref="ReminderPlanner.ReminderHour"/>). À reporter dans le libellé du réglage.</summary>
    public const int CheckHour = 8;

    private const char Separator = '\n';

    /// <summary>Prochain contrôle : <see cref="CheckHour"/> h aujourd'hui, ou demain si cette heure est (presque) passée.</summary>
    public static DateTimeOffset NextRun(TimeProvider clock)
    {
        var now = clock.GetLocalNow();
        var next = At(now.Date);
        return next <= now.AddMinutes(1) ? At(now.Date.AddDays(1)) : next;

        DateTimeOffset At(DateTime day)
        {
            var local = day.AddHours(CheckHour);
            return new DateTimeOffset(local, clock.LocalTimeZone.GetUtcOffset(local));
        }
    }

    /// <summary>
    /// Prochain contrôle à programmer au premier plan. Comme <see cref="NextRun(TimeProvider)"/>, sauf quand le contrôle d'aujourd'hui
    /// n'a pas eu lieu (alarme retardée par le mode économie d'énergie, ou remplacée par une ouverture de l'application juste avant
    /// l'heure) alors que <see cref="NextRun(TimeProvider)"/> repousse à demain : il est alors lancé dans la minute, au lieu d'être perdu.
    /// Sans contrôle déjà lancé (première utilisation), rien n'est rattrapé.
    /// </summary>
    public static DateTimeOffset NextRun(TimeProvider clock, string lastCheckDay)
    {
        var now = clock.GetLocalNow();
        var next = NextRun(clock);
        var checkedBeforeToday = DateTime.TryParseExact(lastCheckDay, DayFormat, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var last) && last.Date < now.Date;
        return checkedBeforeToday && next.Date > now.Date ? now.AddMinutes(1) : next;
    }

    private const string DayFormat = "yyyy-MM-dd";

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // Noté avant la préparation : celle-ci reprogramme le prochain contrôle, qui doit déjà voir celui d'aujourd'hui comme lancé.
        settings.LastDailyCheckDay = clock.GetLocalNow().ToString(DayFormat, System.Globalization.CultureInfo.InvariantCulture);
        // Le même travail qu'au déverrouillage : base ouverte, échéances rattrapées, rappels et prochain contrôle programmés.
        await startup.PrepareInBackgroundAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.BackgroundAlertsEnabled) return;

        var feed = await notifications.LoadAsync(cancellationToken).ConfigureAwait(false);

        // Les prélèvements à venir sont déjà couverts par les rappels d'échéance (réglables abonnement par abonnement).
        var current = feed.Items.Where(x => x.Topic != NotificationTopic.SubscriptionDue).ToList();
        var pushed = settings.PushedNotifications.Split(Separator, StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);

        // Une alerte déjà consultée dans l'application, ou déjà notifiée, ne l'est pas une seconde fois.
        var fresh = current.Where(x => feed.UnreadKeys.Contains(x.Key) && !pushed.Contains(x.Key)).ToList();
        if (fresh.Count > 0 && !await publisher.PublishAsync(fresh, cancellationToken).ConfigureAwait(false)) return;

        // Seules les alertes actuelles sont retenues : celles d'un mois passé disparaissent d'elles-mêmes.
        settings.PushedNotifications = string.Join(Separator, current.Select(x => x.Key.Replace(Separator, ' ')));
    }
}
