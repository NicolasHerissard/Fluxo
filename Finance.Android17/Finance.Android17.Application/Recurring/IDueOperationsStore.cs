namespace Finance.Android17.Application;

public interface IDueOperationsStore
{
    /// <summary>
    /// Enregistre en une seule transaction les dépenses et revenus échus du plan (les dépenses dans la catégorie
    /// « Abonnements », créée si besoin) et avance les prochaines échéances. Un plan vide ne touche à rien.
    /// </summary>
    Task ApplyAsync(DueOperationsPlan plan, CancellationToken cancellationToken = default);
}
