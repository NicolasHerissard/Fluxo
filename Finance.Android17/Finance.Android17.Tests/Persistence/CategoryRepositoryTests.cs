using Finance.Android17.Domain;
using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Persistence;

public sealed class CategoryRepositoryTests : IDisposable
{
    private readonly InfrastructureHost _host = new();

    public void Dispose() => _host.Dispose();

    private async Task<Category> CategoryAsync(string name) =>
        (await _host.Categories.GetAllAsync()).Single(x => x.Name == name);

    [Fact]
    public async Task GetAll_IsSortedByName()
    {
        await _host.Categories.AddAsync("Animaux", "#000000");

        Assert.Equal(["Alimentation", "Animaux", "Autre", "Logement", "Loisirs", "Transport"], (await _host.Categories.GetAllAsync()).Select(x => x.Name));
    }

    [Fact]
    public async Task Categories_CanBeAddedUpdatedAndGetABudget()
    {
        await _host.Categories.AddAsync("  Sport ", "#112233", 5000);
        var sport = await CategoryAsync("Sport");
        Assert.Equal("#112233", sport.Color);
        Assert.Equal(5000, sport.BudgetMinor);

        await _host.Categories.UpdateAsync(sport.Id, "Sports", "#445566", null);

        var updated = await CategoryAsync("Sports");
        Assert.Equal("#445566", updated.Color);
        Assert.Null(updated.BudgetMinor);
        Assert.NotEqual(sport.Version, updated.Version);
    }

    [Fact]
    public async Task Delete_IsRefusedWhileAnActiveExpenseUsesTheCategory_ThenAllowed()
    {
        var food = await CategoryAsync("Alimentation");
        await _host.Expenses.AddAsync(1250, "Pain", new DateTime(2026, 10, 2), food.Id);
        var expenseId = Assert.Single(await _host.Expenses.GetAllAsync()).Id;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _host.Categories.DeleteAsync(food.Id));
        Assert.Equal("Cette catégorie est utilisée par une transaction.", error.Message);

        await _host.Expenses.DeleteAsync(expenseId);
        await _host.Categories.DeleteAsync(food.Id);
        Assert.DoesNotContain(await _host.Categories.GetAllAsync(), x => x.Id == food.Id);
    }

    [Fact]
    public async Task Delete_RenewsTheVersion()
    {
        var sport = await AddSportAsync();
        var before = await _host.ScalarAsync<string>("SELECT Version FROM Categories WHERE Name = 'Sport'");

        await _host.Categories.DeleteAsync(sport.Id);

        Assert.NotEqual(before, await _host.ScalarAsync<string>("SELECT Version FROM Categories WHERE Name = 'Sport'"));
    }

    private async Task<Category> AddSportAsync()
    {
        await _host.Categories.AddAsync("Sport", "#112233");
        return await CategoryAsync("Sport");
    }
}
