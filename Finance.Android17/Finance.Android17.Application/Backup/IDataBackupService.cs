namespace Finance.Android17.Application;

/// <summary>Sauvegarde chiffrée de toutes les données de l'application : transactions, catégories, abonnements, revenus, objectifs, solde initial et réglages.</summary>
public interface IDataBackupService
{
    Task ExportEncryptedAsync(string password, CancellationToken cancellationToken = default);

    /// <summary>Remplace les données et réglages actuels par ceux d'un fichier choisi ; renvoie <see langword="false"/> si l'utilisateur annule.</summary>
    Task<bool> ImportEncryptedAsync(string password, CancellationToken cancellationToken = default);
}
