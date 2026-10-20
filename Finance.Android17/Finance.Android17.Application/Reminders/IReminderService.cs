namespace Finance.Android17.Application;

public interface IReminderService
{
    /// <summary>Programme les alarmes du plan et affiche sa notification du jour (une seule fois par clé).</summary>
    Task ApplyAsync(ReminderPlan plan, CancellationToken cancellationToken = default);
}
