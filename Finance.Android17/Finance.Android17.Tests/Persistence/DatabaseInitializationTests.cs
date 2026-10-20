using Finance.Android17.Application;
using Finance.Android17.Domain;
using Finance.Android17.Tests.Support;
using Microsoft.Data.Sqlite;

namespace Finance.Android17.Tests.Persistence;

public sealed class DatabaseInitializationTests : IDisposable
{
    private readonly InfrastructureHost _host = new();

    public void Dispose() => _host.Dispose();

    private static readonly string[] DefaultNames = ["Alimentation", "Autre", "Logement", "Loisirs", "Transport"];

    [Fact]
    public async Task Initialize_SeedsTheFiveDefaultCategories_OnlyOnce()
    {
        await _host.Database.InitializeAsync();
        await _host.Database.InitializeAsync();
        using var restarted = new InfrastructureHost(databaseOf: _host, secrets: _host.Secrets);

        var names = (await restarted.Categories.GetAllAsync()).Select(x => x.Name).ToList();

        Assert.Equal(DefaultNames, names);
    }

    [Fact]
    public async Task Initialize_IsLazy_EveryRepositoryOpensTheDatabaseOnFirstUse()
    {
        Assert.False(File.Exists(_host.DatabaseFile.Options.DatabasePath));

        await _host.Expenses.GetAllAsync();

        Assert.True(File.Exists(_host.DatabaseFile.Options.DatabasePath));
        Assert.Equal(DefaultNames, (await _host.Categories.GetAllAsync()).Select(x => x.Name));
    }

    [Fact]
    public async Task Initialize_SurvivesManyConcurrentFirstCalls()
    {
        var categoryCounts = Enumerable.Range(0, 6).Select(_ => Task.Run(async () => (await _host.Categories.GetAllAsync()).Count)).ToList();
        var expenseCounts = Enumerable.Range(0, 6).Select(_ => Task.Run(async () => (await _host.Expenses.GetAllAsync()).Count)).ToList();
        var initializations = Enumerable.Range(0, 6).Select(_ => Task.Run(() => _host.Database.InitializeAsync())).ToList();

        await Task.WhenAll(categoryCounts.Concat(expenseCounts).Cast<Task>().Concat(initializations));

        Assert.All(categoryCounts, task => Assert.Equal(5, task.Result));
        Assert.All(expenseCounts, task => Assert.Equal(0, task.Result));
        Assert.Equal(5, (await _host.Categories.GetAllAsync()).Count);
    }

    [Fact]
    public async Task Initialize_CanBeRetried_AfterAFailure()
    {
        var secrets = new FakeSecureValueStore();
        using var host = new InfrastructureHost(secrets: secrets);
        var wrongKeyFile = host.DatabaseFile.Options.DatabasePath;
        await File.WriteAllTextAsync(wrongKeyFile, "ceci n'est pas une base SQLCipher, mais un fichier quelconque assez long pour ne pas être vide");

        await Assert.ThrowsAsync<SqliteException>(() => host.Database.InitializeAsync());

        SqliteConnection.ClearAllPools();
        File.Delete(wrongKeyFile);
        await host.Database.InitializeAsync();
        Assert.Equal(DefaultNames, (await host.Categories.GetAllAsync()).Select(x => x.Name));
    }

    [Fact]
    public async Task Database_IsEncrypted_AndUnreadableWithAnotherKey()
    {
        await _host.Database.InitializeAsync();
        await using (var stream = File.OpenRead(_host.DatabaseFile.Options.DatabasePath))
        {
            var header = new byte[15];
            await stream.ReadExactlyAsync(header);
            Assert.NotEqual("SQLite format 3", System.Text.Encoding.ASCII.GetString(header));
        }

        var otherKeys = new FakeSecureValueStore();
        otherKeys.Values["database_key_v2"] = "une-autre-cle-0123456789abcdef0123456789=";
        using var intruder = new InfrastructureHost(databaseOf: _host, secrets: otherKeys);

        await Assert.ThrowsAsync<SqliteException>(() => intruder.Categories.GetAllAsync());
    }

    [Fact]
    public async Task Data_SurvivesReopeningTheDatabase()
    {
        var food = (await _host.Categories.GetAllAsync()).Single(x => x.Name == "Alimentation");
        await _host.Expenses.AddAsync(1250, "Pain", new DateTime(2026, 10, 2), food.Id);

        using var restarted = new InfrastructureHost(databaseOf: _host, secrets: _host.Secrets);

        var expense = Assert.Single(await restarted.Expenses.GetAllAsync());
        Assert.Equal(1250, expense.AmountMinor);
    }

    [Fact]
    public async Task EveryEntityOfTheModel_CanBeWrittenAndRead()
    {
        var food = (await _host.Categories.GetAllAsync()).Single(x => x.Name == "Alimentation");
        await _host.Expenses.AddAsync(1, "a", new DateTime(2026, 10, 1), food.Id);
        await _host.Incomes.AddAsync(1, "b", new DateTime(2026, 10, 1));
        await _host.RecurringIncomes.AddAsync("c", 1, 1, new DateTime(2026, 11, 1));
        await _host.Subscriptions.AddAsync("d", 1, 1, new DateTime(2026, 11, 1));
        await _host.SavingsGoals.AddAsync("e", 1, 0, null);
        await _host.Profile.SetInitialBalanceAsync(1);

        Assert.Single(await _host.Expenses.GetAllAsync());
        Assert.Single(await _host.Incomes.GetAllAsync());
        Assert.Single(await _host.RecurringIncomes.GetAllAsync());
        Assert.Single(await _host.Subscriptions.GetAllAsync());
        Assert.Single(await _host.SavingsGoals.GetAllAsync());
        Assert.Equal(1, await _host.Profile.GetInitialBalanceAsync());
    }
}
