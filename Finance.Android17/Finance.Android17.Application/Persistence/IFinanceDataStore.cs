namespace Finance.Android17.Application;

/// <summary>Opérations qui portent sur toutes les données financières à la fois.</summary>
public interface IFinanceDataStore
{
    /// <summary>Instantané des données non supprimées (sauvegarde au format 2).</summary>
    Task<FinanceBackupSnapshot> CreateSnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary>Remplace toutes les données par celles de l'instantané, en une transaction (formats 1 et 2).</summary>
    /// <exception cref="InvalidOperationException">Format de sauvegarde inconnu : rien n'est modifié.</exception>
    Task RestoreSnapshotAsync(FinanceBackupSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>Efface tout, remet le solde initial à zéro et recrée les catégories par défaut.</summary>
    Task DeleteAllAsync(CancellationToken cancellationToken = default);
}
