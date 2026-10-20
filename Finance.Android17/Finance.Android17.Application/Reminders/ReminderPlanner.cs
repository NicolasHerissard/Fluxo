using System.Globalization;
using Finance.Android17.Domain;

namespace Finance.Android17.Application;

/// <summary>Règles des rappels d'échéance : quand sonner, et quoi afficher tout de suite.</summary>
public static class ReminderPlanner
{
    /// <summary>Heure locale à laquelle sonne le rappel, le jour J − N.</summary>
    public const int ReminderHour = 9;

    /// <summary>Titre de toutes les notifications d'échéance (immédiate ou programmée).</summary>
    public const string NoticeTitle = "Échéance à venir";

    public static ReminderPlan Plan(IEnumerable<Subscription> subscriptions, int reminderDays, TimeProvider clock)
    {
        var days = Math.Max(0, reminderDays);
        var now = clock.GetLocalNow();
        var today = now.Date;
        var enabled = subscriptions.Where(x => x.ReminderEnabled).ToList();

        var alarms = new List<ScheduledReminder>();
        foreach (var subscription in enabled)
        {
            var trigger = subscription.NextBillingDate.Date.AddDays(-days).AddHours(ReminderHour);
            var triggerAt = new DateTimeOffset(trigger, clock.LocalTimeZone.GetUtcOffset(trigger));
            if (triggerAt <= now) continue;
            alarms.Add(new ScheduledReminder(subscription.Id, Message(subscription), triggerAt));
        }

        var upcoming = enabled.Where(x => x.NextBillingDate.Date >= today && x.NextBillingDate.Date <= today.AddDays(days)).ToList();
        if (upcoming.Count == 0) return new ReminderPlan(alarms, null);
        var text = upcoming.Count == 1
            ? Message(upcoming[0])
            : $"{upcoming.Count} abonnements arrivent dans les {reminderDays} prochains jours.";
        var key = $"subscription_reminder_{today.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}";
        return new ReminderPlan(alarms, new ReminderNotice(key, NoticeTitle, text));
    }

    private static string Message(Subscription subscription) =>
        $"{subscription.Name} arrive le {subscription.NextBillingDate.ToString("dd/MM", CultureInfo.InvariantCulture)}.";
}
