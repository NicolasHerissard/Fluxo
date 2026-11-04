using Finance.Android17.Application;

namespace Finance.Android17.Infrastructure;

/// <summary>Écarte une base dont la clé est perdue : le fichier est renommé (jamais supprimé), puis la prochaine ouverture crée clé et base neuves.</summary>
internal sealed class DatabaseRecovery(ISecureValueStore secrets, FinanceDatabaseOptions options, TimeProvider clock) : IDatabaseRecovery
{
    public async Task StartOverAsync(CancellationToken cancellationToken = default)
    {
        var existing = await secrets.GetAsync(DatabaseKeyProvider.StorageKey, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(existing))
            throw new InvalidOperationException("La clé de la base est disponible : il n'y a rien à réinitialiser.");

        var stamp = clock.GetUtcNow().ToString("yyyyMMddHHmmss");
        foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
        {
            var path = options.DatabasePath + suffix;
            if (File.Exists(path)) File.Move(path, $"{path}.illisible-{stamp}");
        }
    }
}
