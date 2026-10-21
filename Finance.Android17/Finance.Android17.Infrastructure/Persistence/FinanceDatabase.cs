using Finance.Android17.Application;
using Finance.Android17.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Finance.Android17.Infrastructure;

/// <summary>
/// Point d'entrée unique vers la base SQLCipher. Elle est ouverte, mise à niveau (<see cref="FinanceSchema"/>) et
/// garnie des catégories par défaut une seule fois, avant que le premier contexte ne soit remis ; les dépôts ne
/// s'occupent donc jamais de l'initialisation.
/// </summary>
internal sealed class FinanceDatabase(ISecretStore secrets, FinanceDatabaseOptions options, TimeProvider clock) : IDatabaseInitializer
{
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private volatile DbContextOptions<FinanceDbContext>? _contextOptions;

    /// <summary>Instant courant, de l'horloge injectée : c'est lui qui date créations, modifications et suppressions.</summary>
    public DateTime UtcNow => clock.GetUtcNow().UtcDateTime;

    public async Task InitializeAsync(CancellationToken cancellationToken = default) =>
        _ = await ContextOptionsAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>Nouveau contexte sur la base prête à l'emploi ; à libérer par l'appelant.</summary>
    public async Task<FinanceDbContext> OpenAsync(CancellationToken cancellationToken = default) =>
        new(await ContextOptionsAsync(cancellationToken).ConfigureAwait(false));

    private async Task<DbContextOptions<FinanceDbContext>> ContextOptionsAsync(CancellationToken cancellationToken)
    {
        if (_contextOptions is { } ready) return ready;
        await _initializationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_contextOptions is { } readyNow) return readyNow;
            SQLitePCL.Batteries_V2.Init();
            var key = await secrets.GetOrCreateDatabaseKeyAsync(cancellationToken).ConfigureAwait(false);
            var dataDirectory = Path.GetDirectoryName(Path.GetFullPath(options.DatabasePath));
            if (!string.IsNullOrEmpty(dataDirectory)) Directory.CreateDirectory(dataDirectory);
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = options.DatabasePath,
                Password = key,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();

            await using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                await FinanceSchema.UpgradeAsync(connection, cancellationToken).ConfigureAwait(false);
            }

            var contextOptions = new DbContextOptionsBuilder<FinanceDbContext>().UseSqlite(connectionString).Options;
            await using (var db = new FinanceDbContext(contextOptions))
            {
                if (!await db.Categories.AnyAsync(cancellationToken).ConfigureAwait(false))
                {
                    db.Categories.AddRange(DefaultCategories.CreateInitialSet());
                    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            // Publié en dernier : un autre appelant ne voit jamais une base à moitié préparée.
            _contextOptions = contextOptions;
            return contextOptions;
        }
        finally
        {
            _initializationLock.Release();
        }
    }
}
