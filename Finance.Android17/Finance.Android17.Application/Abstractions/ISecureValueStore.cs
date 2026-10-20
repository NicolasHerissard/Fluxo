namespace Finance.Android17.Application;

/// <summary>Coffre à valeurs de la plateforme (Keystore Android, coffre Windows…) pour les secrets de l'application.</summary>
public interface ISecureValueStore
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task SetAsync(string key, string value, CancellationToken cancellationToken = default);
}
