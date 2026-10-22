using Finance.Android17.Domain;

namespace Finance.Android17.Application;

/// <summary>Programme les rappels d'abonnements si l'utilisateur les a activés, et annule ceux qui ne sont plus voulus.</summary>
public sealed class SubscriptionReminderScheduler(ISettingsService settings, IReminderService reminders, TimeProvider clock)
{
    public bool IsEnabled => settings.SubscriptionRemindersEnabled;

    public Task RefreshAsync(IReadOnlyList<Subscription> subscriptions, CancellationToken cancellationToken = default)
    {
        // Rappels désactivés : un plan vide annule les alarmes déjà programmées.
        if (!IsEnabled) return reminders.ApplyAsync(ReminderPlan.Empty, cancellationToken);
        var plan = ReminderPlanner.Plan(subscriptions, settings.SubscriptionReminderDays, clock);
        return reminders.ApplyAsync(plan, cancellationToken);
    }
}
