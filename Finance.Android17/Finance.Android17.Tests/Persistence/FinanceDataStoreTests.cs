using Finance.Android17.Application;
using Finance.Android17.Domain;
using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Persistence;

public sealed class FinanceDataStoreTests : IDisposable
{
    private readonly InfrastructureHost _host = new();

    public void Dispose() => _host.Dispose();

    private async Task<Category> CategoryAsync(string name) =>
        (await _host.Categories.GetAllAsync()).Single(x => x.Name == name);

    private async Task SeedAllKindsOfDataAsync()
    {
        var food = await CategoryAsync("Alimentation");
        await _host.Categories.AddAsync("Sport", "#112233", 5000);
        await _host.Expenses.AddAsync(1250, "Pain", new DateTime(2026, 10, 2), food.Id, "EUR");
        await _host.Expenses.AddAsync(999, "Supprimée", new DateTime(2026, 10, 3), food.Id);
        await _host.Expenses.DeleteAsync((await _host.Expenses.GetAllAsync()).Single(x => x.Description == "Supprimée").Id);
        await _host.Incomes.AddAsync(250_000, "Salaire", new DateTime(2026, 10, 1));
        await _host.RecurringIncomes.AddAsync("Loyer perçu", 50_000, 3, new DateTime(2026, 12, 1));
        await _host.SavingsGoals.AddAsync("Vacances", 200_000, 50_000, new DateTime(2027, 6, 1));
        await _host.Subscriptions.AddAsync("Netflix", 1299, 1, new DateTime(2026, 11, 15));
        await _host.Profile.SetInitialBalanceAsync(100_000);
    }

    [Fact]
    public async Task Snapshot_HoldsEverythingExceptDeletedData_InFormat2()
    {
        await SeedAllKindsOfDataAsync();

        var snapshot = await _host.Data.CreateSnapshotAsync();

        Assert.Equal(2, snapshot.FormatVersion);
        Assert.Equal(_host.Clock.Now, snapshot.CreatedAtUtc, TimeSpan.FromMilliseconds(1));
        Assert.Equal(6, snapshot.Categories.Count);
        Assert.Equal("Pain", Assert.Single(snapshot.Expenses).Description);
        Assert.Equal("Salaire", Assert.Single(snapshot.Incomes!).Description);
        Assert.Equal(3, Assert.Single(snapshot.RecurringIncomes!).IntervalMonths);
        Assert.Equal("Vacances", Assert.Single(snapshot.SavingsGoals!).Name);
        Assert.Equal("Netflix", Assert.Single(snapshot.Subscriptions).Name);
        Assert.Equal(100_000, snapshot.InitialBalanceMinor);
    }

    [Fact]
    public async Task RoundTrip_RestoresEverything_IntoAnotherDatabase()
    {
        await SeedAllKindsOfDataAsync();
        var snapshot = await _host.Data.CreateSnapshotAsync();

        using var other = new InfrastructureHost();
        await other.Data.RestoreSnapshotAsync(snapshot);

        Assert.Equal(await _host.Profile.GetInitialBalanceAsync(), await other.Profile.GetInitialBalanceAsync());
        Assert.Equal((await _host.Categories.GetAllAsync()).Select(x => (x.Id, x.Name, x.Color, x.BudgetMinor)),
            (await other.Categories.GetAllAsync()).Select(x => (x.Id, x.Name, x.Color, x.BudgetMinor)));
        var expense = Assert.Single(await other.Expenses.GetAllAsync());
        Assert.Equal(("Pain", 1250L), (expense.Description, expense.AmountMinor));
        Assert.Equal("Alimentation", expense.Category?.Name);
        Assert.Equal("Salaire", Assert.Single(await other.Incomes.GetAllAsync()).Description);
        Assert.Equal(3, Assert.Single(await other.RecurringIncomes.GetAllAsync()).IntervalMonths);
        Assert.Equal(0.25, Assert.Single(await other.SavingsGoals.GetAllAsync()).Progress, 5);
        Assert.Equal(new DateTime(2026, 11, 15), Assert.Single(await other.Subscriptions.GetAllAsync()).NextBillingDate);
    }

    [Fact]
    public async Task Restore_ReplacesExistingData()
    {
        await SeedAllKindsOfDataAsync();
        var snapshot = await _host.Data.CreateSnapshotAsync();
        var food = await CategoryAsync("Alimentation");
        await _host.Expenses.AddAsync(5, "Ajoutée après la sauvegarde", new DateTime(2026, 10, 9), food.Id);

        await _host.Data.RestoreSnapshotAsync(snapshot);

        Assert.Equal("Pain", Assert.Single(await _host.Expenses.GetAllAsync()).Description);
    }

    [Fact]
    public async Task Restore_Format1_StartsNewDataEmpty_AndResetsTheInitialBalance()
    {
        await SeedAllKindsOfDataAsync();
        var category = (await _host.Categories.GetAllAsync()).First();
        var v1 = new FinanceBackupSnapshot(1, DateTime.UtcNow,
            [new CategoryBackup(category.Id, category.Name, category.Color, null, DateTime.UtcNow)],
            [new ExpenseBackup(Guid.NewGuid(), 700, "EUR", "Ancienne dépense", new DateTime(2026, 1, 5), category.Id, DateTime.UtcNow)],
            []);

        await _host.Data.RestoreSnapshotAsync(v1);

        Assert.Equal("Ancienne dépense", Assert.Single(await _host.Expenses.GetAllAsync()).Description);
        Assert.Empty(await _host.Incomes.GetAllAsync());
        Assert.Empty(await _host.RecurringIncomes.GetAllAsync());
        Assert.Empty(await _host.SavingsGoals.GetAllAsync());
        Assert.Equal(0, await _host.Profile.GetInitialBalanceAsync());
    }

    [Fact]
    public async Task Restore_RefusesUnknownFormats_AndLeavesTheDataUntouched()
    {
        await SeedAllKindsOfDataAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _host.Data.RestoreSnapshotAsync(new FinanceBackupSnapshot(3, DateTime.UtcNow, [], [], [])));

        Assert.Single(await _host.Expenses.GetAllAsync());
    }

    [Fact]
    public async Task Restore_ThatFails_IsRolledBackEntirely()
    {
        await SeedAllKindsOfDataAsync();
        var broken = new FinanceBackupSnapshot(2, DateTime.UtcNow,
            [new CategoryBackup(Guid.NewGuid(), "Seule", "#000000", null, DateTime.UtcNow)],
            [new ExpenseBackup(Guid.NewGuid(), 700, "EUR", "Orpheline", new DateTime(2026, 1, 5), Guid.NewGuid(), DateTime.UtcNow)],
            []);

        await Assert.ThrowsAnyAsync<Exception>(() => _host.Data.RestoreSnapshotAsync(broken));

        Assert.Equal("Pain", Assert.Single(await _host.Expenses.GetAllAsync()).Description);
        Assert.Equal(6, (await _host.Categories.GetAllAsync()).Count);
        Assert.Equal(100_000, await _host.Profile.GetInitialBalanceAsync());
    }

    [Fact]
    public async Task DeleteAll_RemovesEverything_AndRecreatesTheDefaultCategories()
    {
        await SeedAllKindsOfDataAsync();

        await _host.Data.DeleteAllAsync();

        Assert.Empty(await _host.Expenses.GetAllAsync());
        Assert.Empty(await _host.Incomes.GetAllAsync());
        Assert.Empty(await _host.RecurringIncomes.GetAllAsync());
        Assert.Empty(await _host.Subscriptions.GetAllAsync());
        Assert.Empty(await _host.SavingsGoals.GetAllAsync());
        Assert.Equal(0, await _host.Profile.GetInitialBalanceAsync());
        Assert.Equal(["Alimentation", "Autre", "Logement", "Loisirs", "Transport"], (await _host.Categories.GetAllAsync()).Select(x => x.Name));
    }
}
