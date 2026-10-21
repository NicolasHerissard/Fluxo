namespace Finance.Android17.Application;

public interface IReminderService
{
    /// <summary>
    /// Fait correspondre les alarmes de la plateforme au plan : programme celles du plan, annule celles qui n'y sont plus
    /// (un plan vide les annule toutes) et affiche la notification du jour (une seule fois par clé).
    /// </summary>
    Task ApplyAsync(ReminderPlan plan, CancellationToken cancellationToken = default);
}
