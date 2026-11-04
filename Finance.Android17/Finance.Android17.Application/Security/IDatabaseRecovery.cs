namespace Finance.Android17.Application;

/// <summary>Sortie de secours quand la clé de la base est définitivement perdue.</summary>
public interface IDatabaseRecovery
{
    /// <summary>
    /// Met de côté la base illisible (elle n'est pas supprimée : si la clé revient, le fichier peut être récupéré) pour que l'application
    /// démarre sur une base neuve. Refusé si la clé est en réalité disponible. Les données se retrouvent ensuite par une sauvegarde chiffrée.
    /// </summary>
    Task StartOverAsync(CancellationToken cancellationToken = default);
}
