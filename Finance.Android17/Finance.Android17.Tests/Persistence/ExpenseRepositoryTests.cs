using Finance.Android17.Domain;
using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Persistence;

public sealed class ExpenseRepositoryTests : IDisposable
{
    private readonly InfrastructureHost _host = new();

    public void Dispose() => _host.Dispose();

    private async Task<Category> CategoryAsync(string name) =>
        (await _host.Categories.GetAllAsync()).Single(x => x.Name == name);

    [Fact]
    public async Task Add_TrimsDescription_DropsTheTime_AndLoadsTheCategory()
    {
        var food = await CategoryAsync("Alimentation");

        await _host.Expenses.AddAsync(1250, "  Pain  ", new DateTime(2026, 10, 2, 17, 45, 0), food.Id, "USD");

        var expense = Assert.Single(await _host.Expenses.GetAllAsync());
        Assert.Equal("Pain", expense.Description);
        Assert.Equal(new DateTime(2026, 10, 2), expense.OccurredOn);
        Assert.Equal("USD", expense.CurrencyCode);
        Assert.Equal("Alimentation", expense.Category?.Name);
    }

    [Fact]
    public async Task Add_DatesTheExpenseWithTheInjectedClock()
    {
        var food = await CategoryAsync("Alimentation");

        await _host.Expenses.AddAsync(1250, "Pain", new DateTime(2026, 10, 2), food.Id);

        var expense = Assert.Single(await _host.Expenses.GetAllAsync());
        var now = new DateTime(2026, 10, 20, 9, 30, 0, DateTimeKind.Utc);
        Assert.Equal(now, expense.CreatedAtUtc, TimeSpan.FromMilliseconds(1));
        Assert.Equal(now, expense.UpdatedAtUtc, TimeSpan.FromMilliseconds(1));
        Assert.Null(expense.DeletedAtUtc);
    }

    [Fact]
    public async Task GetAll_SortsByDateThenByCreation_MostRecentFirst()
    {
        var food = await CategoryAsync("Alimentation");
        await _host.Expenses.AddAsync(100, "ancienne", new DateTime(2026, 9, 1), food.Id);
        await _host.Expenses.AddAsync(200, "recente-1", new DateTime(2026, 10, 1), food.Id);
        _host.Clock.Now = _host.Clock.Now.AddMinutes(1);
        await _host.Expenses.AddAsync(300, "recente-2", new DateTime(2026, 10, 1), food.Id);

        var descriptions = (await _host.Expenses.GetAllAsync()).Select(x => x.Description).ToList();

        Assert.Equal(["recente-2", "recente-1", "ancienne"], descriptions);
    }

    [Fact]
    public async Task Update_ChangesTheFields_AndRenewsTheVersion()
    {
        var food = await CategoryAsync("Alimentation");
        var transport = await CategoryAsync("Transport");
        await _host.Expenses.AddAsync(1250, "Pain", new DateTime(2026, 10, 2), food.Id);
        var before = Assert.Single(await _host.Expenses.GetAllAsync());
        _host.Clock.Now = _host.Clock.Now.AddHours(2);

        await _host.Expenses.UpdateAsync(before.Id, 999, "  Train ", new DateTime(2026, 10, 5), transport.Id, "CHF");

        var after = Assert.Single(await _host.Expenses.GetAllAsync());
        Assert.Equal(999, after.AmountMinor);
        Assert.Equal("Train", after.Description);
        Assert.Equal(new DateTime(2026, 10, 5), after.OccurredOn);
        Assert.Equal("Transport", after.Category?.Name);
        Assert.Equal("CHF", after.CurrencyCode);
        Assert.NotEqual(before.Version, after.Version);
        Assert.Equal(before.CreatedAtUtc, after.CreatedAtUtc);
        Assert.Equal(_host.Clock.Now, after.UpdatedAtUtc, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Update_OfAnUnknownOrDeletedExpense_ChangesNothing()
    {
        var food = await CategoryAsync("Alimentation");
        await _host.Expenses.AddAsync(1250, "Pain", new DateTime(2026, 10, 2), food.Id);
        var id = Assert.Single(await _host.Expenses.GetAllAsync()).Id;
        await _host.Expenses.DeleteAsync(id);

        await _host.Expenses.UpdateAsync(Guid.NewGuid(), 1, "x", new DateTime(2026, 1, 1), food.Id);
        await _host.Expenses.UpdateAsync(id, 1, "x", new DateTime(2026, 1, 1), food.Id);
        await _host.Expenses.RestoreAsync(id);

        var expense = Assert.Single(await _host.Expenses.GetAllAsync());
        Assert.Equal(("Pain", 1250L), (expense.Description, expense.AmountMinor));
    }

    [Fact]
    public async Task Delete_HidesTheExpense_AndRestoreBringsItBack()
    {
        var food = await CategoryAsync("Alimentation");
        await _host.Expenses.AddAsync(1250, "Pain", new DateTime(2026, 10, 2), food.Id);
        var id = Assert.Single(await _host.Expenses.GetAllAsync()).Id;
        var versionAtCreation = await _host.ScalarAsync<string>("SELECT Version FROM Expenses");

        await _host.Expenses.DeleteAsync(id);
        Assert.Empty(await _host.Expenses.GetAllAsync());
        Assert.Equal(1, await _host.ScalarAsync<long>("SELECT COUNT(*) FROM Expenses WHERE DeletedAtUtc IS NOT NULL"));
        var versionAtDeletion = await _host.ScalarAsync<string>("SELECT Version FROM Expenses");
        Assert.NotEqual(versionAtCreation, versionAtDeletion);

        await _host.Expenses.RestoreAsync(id);
        Assert.Single(await _host.Expenses.GetAllAsync());
        Assert.NotEqual(versionAtDeletion, await _host.ScalarAsync<string>("SELECT Version FROM Expenses"));
    }
}
