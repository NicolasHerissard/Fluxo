using Finance.Android17.Application;
using Finance.Android17.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Android17.Tests.Support;

/// <summary>
/// Infrastructure composée par <c>AddInfrastructure</c> comme dans l'application, avec un coffre à secrets et un
/// échange de fichiers factices et une horloge figée.
/// </summary>
public sealed class InfrastructureHost : IDisposable
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
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(now ?? AppFixture.Now));
        services.AddSingleton<ISecureValueStore>(Secrets);
        services.AddSingleton<IFileExchange>(Files);
        services.AddInfrastructure(_db.Options);
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    public FakeSecureValueStore Secrets { get; }
    public FakeFileExchange Files { get; } = new();

    public T Get<T>() where T : notnull => _provider.GetRequiredService<T>();

    public void Dispose()
    {
        _provider.Dispose();
        if (_ownsDatabase) _db.Dispose();
    }
}
