namespace Finance.Android17.Application;

/// <summary>Préparation des données après le déverrouillage : base ouverte, échéances rattrapées, rappels programmés.</summary>
public sealed class StartupService(IFinanceStore store, DueOperationsService dueOperations, SubscriptionReminderScheduler reminders)
{
    public async Task PrepareAsync(CancellationToken cancellationToken = default)
    {
        await store.InitializeAsync(cancellationToken).ConfigureAwait(false);
        await dueOperations.ApplyAsync(cancellationToken).ConfigureAwait(false);
        // Lus après le rattrapage : les prochaines échéances ont avancé.
        if (reminders.IsEnabled)
            await reminders.RefreshAsync(await store.GetSubscriptionsAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
    }
}
