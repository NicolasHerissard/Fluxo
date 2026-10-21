using Finance.Android17.Domain;

namespace Finance.Android17.Application;

/// <summary>Programme les rappels d'abonnements si l'utilisateur les a activés.</summary>
public sealed class SubscriptionReminderScheduler(ISettingsService settings, IReminderService reminders, TimeProvider clock)
{
    public bool IsEnabled => settings.SubscriptionRemindersEnabled;

    public Task RefreshAsync(IReadOnlyList<Subscription> subscriptions, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled) return Task.CompletedTask;
        var plan = ReminderPlanner.Plan(subscriptions, settings.SubscriptionReminderDays, clock);
        return reminders.ApplyAsync(plan, cancellationToken);
    }
}
