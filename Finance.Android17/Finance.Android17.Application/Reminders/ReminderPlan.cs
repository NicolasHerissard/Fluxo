namespace Finance.Android17.Application;

/// <summary>Alarme à programmer : le rappel d'une échéance, à <see cref="TriggerAt"/>.</summary>
public sealed record ScheduledReminder(Guid SubscriptionId, string Message, DateTimeOffset TriggerAt);

/// <summary>Notification immédiate ; <see cref="Key"/> identifie le jour pour ne l'afficher qu'une fois.</summary>
public sealed record ReminderNotice(string Key, string Title, string Text);

public sealed record ReminderPlan(IReadOnlyList<ScheduledReminder> Alarms, ReminderNotice? Notice)
{
    public static ReminderPlan Empty { get; } = new([], null);
}
