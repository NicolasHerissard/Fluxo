namespace Finance.Android17.Application;

/// <summary>Préparation des données après le déverrouillage : base ouverte, échéances rattrapées, rappels et contrôle quotidien programmés.</summary>
public sealed class StartupService(
    IDatabaseInitializer database,
    ISubscriptionRepository subscriptions,
    DueOperationsService dueOperations,
    SubscriptionReminderScheduler reminders,
    IDailyCheckScheduler dailyCheck)
{
    // Le contrôle quotidien peut se déclencher pendant que l'utilisateur déverrouille : deux préparations
    // simultanées rattraperaient chacune les mêmes échéances.
    private readonly SemaphoreSlim _gate = new(1, 1);

    public Task PrepareAsync(CancellationToken cancellationToken = default) => PrepareAsync(background: false, cancellationToken);

    /// <summary>
    /// Comme <see cref="PrepareAsync(CancellationToken)"/>, sans la notification immédiate d'échéance : en arrière-plan,
    /// l'alarme du jour sonne déjà, la montrer deux fois n'apporterait rien.
    /// </summary>
    public Task PrepareInBackgroundAsync(CancellationToken cancellationToken = default) => PrepareAsync(background: true, cancellationToken);

    private async Task PrepareAsync(bool background, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await database.InitializeAsync(cancellationToken).ConfigureAwait(false);
            await dueOperations.ApplyAsync(cancellationToken).ConfigureAwait(false);
            // Lus après le rattrapage : les prochaines échéances ont avancé. Toujours appliqué, même rappels désactivés :
            // c'est ce qui annule les alarmes encore programmées.
            await reminders.RefreshAsync(await subscriptions.GetAllAsync(cancellationToken).ConfigureAwait(false), showNotice: !background, cancellationToken).ConfigureAwait(false);
            await dailyCheck.EnsureScheduledAsync(cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }
}
