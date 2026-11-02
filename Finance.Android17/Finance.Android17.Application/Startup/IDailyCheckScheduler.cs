namespace Finance.Android17.Application;

/// <summary>Port de plateforme : fait déclencher <see cref="DailyCheckService"/> chaque matin, même application fermée.</summary>
public interface IDailyCheckScheduler
{
    /// <summary>
    /// Programme le prochain contrôle (à <see cref="DailyCheckService.NextRun"/>) ; au premier plan, obtient aussi
    /// l'autorisation de notifier quand les alertes en arrière-plan sont activées. Ne lève jamais d'exception.
    /// </summary>
    Task EnsureScheduledAsync(CancellationToken cancellationToken = default);
}
