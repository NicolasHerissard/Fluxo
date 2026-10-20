using Finance.Android17.Application;
using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Security;

public sealed class DatabaseKeyProviderTests
{
    [Fact]
    public async Task FirstCall_CreatesA32ByteKey_AndStoresIt()
    {
        using var host = new InfrastructureHost();

        var key = await host.Get<ISecretStore>().GetOrCreateDatabaseKeyAsync();

        Assert.Equal(32, Convert.FromBase64String(key).Length);
        Assert.Equal(key, host.Secrets.Values["database_key_v2"]);
    }

    [Fact]
    public async Task NextCalls_ReturnTheSameKey()
    {
        using var host = new InfrastructureHost();
        var secrets = host.Get<ISecretStore>();

        var first = await secrets.GetOrCreateDatabaseKeyAsync();
        var second = await secrets.GetOrCreateDatabaseKeyAsync();

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task ExistingKey_IsKeptUntouched()
    {
        var existing = new FakeSecureValueStore { Values = { ["database_key_v2"] = "cle-deja-presente" } };
        using var host = new InfrastructureHost(secrets: existing);

        Assert.Equal("cle-deja-presente", await host.Get<ISecretStore>().GetOrCreateDatabaseKeyAsync());
    }

    [Fact]
    public async Task Database_ReopensWithTheStoredKey_AfterARestart()
    {
        using var host = new InfrastructureHost();
        var food = (await host.Categories.GetAllAsync()).Single(x => x.Name == "Alimentation");
        await host.Expenses.AddAsync(1_250, "Pain", new DateTime(2026, 10, 2), food.Id);

        // Même coffre à secrets, nouveau processus : la base doit s'ouvrir avec la clé conservée.
        using var restarted = new InfrastructureHost(secrets: host.Secrets, databaseOf: host);

        var expense = Assert.Single(await restarted.Expenses.GetAllAsync());
        Assert.Equal("Pain", expense.Description);
    }
}
