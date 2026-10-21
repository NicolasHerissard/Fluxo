using Finance.Android17.Domain;
using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Persistence;

public sealed class DefaultCategoriesTests : IDisposable
{
    private readonly InfrastructureHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void InitialSet_HasFiveDistinctColoredCategories()
    {
        var set = DefaultCategories.CreateInitialSet();

        Assert.Equal(["Alimentation", "Transport", "Logement", "Loisirs", "Autre"], set.Select(x => x.Name));
        Assert.Equal(["#2E7D32", "#1565C0", "#6A1B9A", "#EF6C00", "#546E7A"], set.Select(x => x.Color));
        Assert.Equal(5, set.Select(x => x.Id).Distinct().Count());
    }

    [Fact]
    public void InitialSet_CreatesFreshEntitiesEveryTime()
    {
        Assert.NotEqual(DefaultCategories.CreateInitialSet()[0].Id, DefaultCategories.CreateInitialSet()[0].Id);
    }

    [Fact]
    public void SubscriptionsCategory_IsNotPartOfTheInitialSet()
    {
        Assert.DoesNotContain(DefaultCategories.CreateInitialSet(), x => x.Name == DefaultCategories.SubscriptionsName);
        Assert.Equal(("Abonnements", "#7D5260"), (DefaultCategories.CreateSubscriptions().Name, DefaultCategories.CreateSubscriptions().Color));
    }

    [Fact]
    public async Task Database_SeedsExactlyTheInitialSet_AndRecreatesItAfterDeleteAll()
    {
        var expected = DefaultCategories.CreateInitialSet().Select(x => (x.Name, x.Color)).OrderBy(x => x.Name).ToList();

        Assert.Equal(expected, (await _host.Categories.GetAllAsync()).Select(x => (x.Name, x.Color)));

        await _host.Categories.AddAsync("Perso", "#000000");
        await _host.Data.DeleteAllAsync();

        Assert.Equal(expected, (await _host.Categories.GetAllAsync()).Select(x => (x.Name, x.Color)));
    }
}
