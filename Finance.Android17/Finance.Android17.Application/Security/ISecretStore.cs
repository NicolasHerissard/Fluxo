namespace Finance.Android17.Application;

/// <summary>Fournit la clé de chiffrement de la base (générée au premier lancement, jamais affichée).</summary>
public interface ISecretStore
{
    Task<string> GetOrCreateDatabaseKeyAsync(CancellationToken cancellationToken = default);
}
