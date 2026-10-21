namespace Finance.Android17.Application;

/// <summary>Préparation des données après le déverrouillage : base ouverte, échéances rattrapées, rappels programmés.</summary>
public sealed class StartupService(
    IDatabaseInitializer database,
    ISubscriptionRepository subscriptions,
    DueOperationsService dueOperations,
    SubscriptionReminderScheduler reminders)
{
    public async Task PrepareAsync(CancellationToken cancellationToken = default)
    {
        await database.InitializeAsync(cancellationToken).ConfigureAwait(false);
        await dueOperations.ApplyAsync(cancellationToken).ConfigureAwait(false);
        // Lus après le rattrapage : les prochaines échéances ont avancé. Toujours appliqué, même rappels désactivés :
        // c'est ce qui annule les alarmes encore programmées.
        await reminders.RefreshAsync(await subscriptions.GetAllAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
    }
}
