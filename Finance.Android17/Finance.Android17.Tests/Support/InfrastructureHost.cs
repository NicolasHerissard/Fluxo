using Finance.Android17.Application;
using Finance.Android17.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Android17.Tests.Support;

/// <summary>
/// Infrastructure composée par <c>AddInfrastructure</c> comme dans l'application, avec un coffre à secrets et un
/// échange de fichiers factices et une horloge figée.
/// </summary>
public sealed class InfrastructureHost : FinancePorts, IDisposable
{
    private readonly TempDatabase _db;
    private readonly bool _ownsDatabase;
    private readonly ServiceProvider _provider;

    /// <param name="databaseOf">Autre hôte dont on rouvre la base (simule un redémarrage de l'application).</param>
    public InfrastructureHost(DateTime? now = null, FakeSecureValueStore? secrets = null, InfrastructureHost? databaseOf = null)
    {
        _db = databaseOf?._db ?? new TempDatabase();
        _ownsDatabase = databaseOf is null;
        Secrets = secrets ?? new FakeSecureValueStore();
        var services = new ServiceCollection();
        Clock = new FixedTimeProvider(now ?? AppFixture.Now);
        services.AddSingleton<TimeProvider>(Clock);
        services.AddSingleton<ISecureValueStore>(Secrets);
        services.AddSingleton<IFileExchange>(Files);
        services.AddSingleton<ISettingsService>(Settings);
        services.AddInfrastructure(_db.Options);
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    public FixedTimeProvider Clock { get; }
    public FakeSecureValueStore Secrets { get; }
    public FakeFileExchange Files { get; } = new();
    public FakeSettings Settings { get; } = new();

    /// <summary>Le fichier de base de cet hôte, pour l'inspecter ou le modifier en SQL.</summary>
    public TempDatabase DatabaseFile => _db;

    public override T Get<T>() => _provider.GetRequiredService<T>();

    /// <summary>Exécute du SQL directement sur le fichier de base, avec sa clé : pour fabriquer ou inspecter une base.</summary>
    public async Task ExecuteSqlAsync(params string[] statements)
    {
        await using var connection = await OpenRawAsync();
        foreach (var sql in statements)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }
        await connection.CloseAsync();
        SqliteConnection.ClearAllPools();
    }

    public async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = await OpenRawAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("La requête n'a rien renvoyé.");
        await connection.CloseAsync();
        SqliteConnection.ClearAllPools();
        return (T)Convert.ChangeType(value, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    private async Task<SqliteConnection> OpenRawAsync()
    {
        SqliteConnection.ClearAllPools();
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = DatabaseFile.Options.DatabasePath,
            Password = await Get<ISecretStore>().GetOrCreateDatabaseKeyAsync()
        }.ToString());
        await connection.OpenAsync();
        return connection;
    }

    public void Dispose()
    {
        _provider.Dispose();
        if (_ownsDatabase) _db.Dispose();
    }
}
