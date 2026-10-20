using System.Security.Cryptography;
using Finance.Android17.Application;

namespace Finance.Android17.Infrastructure;

/// <summary>Clé SQLCipher : 32 octets aléatoires créés au premier lancement, puis relus dans le coffre de la plateforme.</summary>
internal sealed class DatabaseKeyProvider(ISecureValueStore secrets) : ISecretStore
{
    internal const string StorageKey = "database_key_v2";
    private const int KeySize = 32;

    public async Task<string> GetOrCreateDatabaseKeyAsync(CancellationToken cancellationToken = default)
    {
        var existing = await secrets.GetAsync(StorageKey, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(existing)) return existing;
        cancellationToken.ThrowIfCancellationRequested();
        var created = Convert.ToBase64String(RandomNumberGenerator.GetBytes(KeySize));
        await secrets.SetAsync(StorageKey, created, cancellationToken).ConfigureAwait(false);
        return created;
    }
}
