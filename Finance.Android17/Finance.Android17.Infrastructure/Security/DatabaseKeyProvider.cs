using System.Security.Cryptography;
using Finance.Android17.Application;

namespace Finance.Android17.Infrastructure;

/// <summary>
/// Clé SQLCipher : 32 octets aléatoires créés au premier lancement, puis relus dans le coffre de la plateforme.
/// Si le coffre ne la restitue plus alors que la base existe déjà, rien n'est écrasé : une nouvelle clé rendrait la
/// base définitivement illisible, alors que l'ancienne peut parfois revenir (déverrouillage du téléphone, coffre restauré).
/// </summary>
internal sealed class DatabaseKeyProvider(ISecureValueStore secrets, FinanceDatabaseOptions options) : ISecretStore
{
    internal const string StorageKey = "database_key_v2";
    private const int KeySize = 32;

    public async Task<string> GetOrCreateDatabaseKeyAsync(CancellationToken cancellationToken = default)
    {
        var existing = await secrets.GetAsync(StorageKey, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(existing)) return existing;
        cancellationToken.ThrowIfCancellationRequested();
        if (DatabaseExists())
            throw new InvalidOperationException("La clé de chiffrement de la base est introuvable alors que la base existe : elle ne peut pas être lue. Vos données n'ont pas été modifiées. Réessayez après avoir déverrouillé le téléphone, ou restaurez une sauvegarde.");
        var created = Convert.ToBase64String(RandomNumberGenerator.GetBytes(KeySize));
        await secrets.SetAsync(StorageKey, created, cancellationToken).ConfigureAwait(false);
        return created;
    }

    // Un fichier vide est un reste de création interrompue : il ne contient aucune donnée à protéger.
    private bool DatabaseExists() => File.Exists(options.DatabasePath) && new FileInfo(options.DatabasePath).Length > 0;
}
