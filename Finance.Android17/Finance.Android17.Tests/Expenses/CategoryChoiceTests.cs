using Finance.Android17.Application;
using Finance.Android17.Domain;
using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Expenses;

public class CategoryChoiceTests
{
    private readonly Category _food = Samples.Category("Alimentation");
    private readonly Category _housing = Samples.Category("Logement");
    private readonly Category _leisure = Samples.Category("Loisirs");
    private readonly Category _transport = Samples.Category("Transport");

    private IReadOnlyList<Category> All => [_food, _housing, _leisure, _transport];

    [Fact]
    public void OrderByRecentUse_PutsTheMostRecentlyUsedFirst_ThenTheOthersByName()
    {
        var expenses = new[]
        {
            Samples.Expense(1, new DateTime(2026, 10, 1), _leisure),
            Samples.Expense(1, new DateTime(2026, 10, 10), _transport),
            Samples.Expense(1, new DateTime(2026, 9, 1), _leisure)
        };

        var ordered = CategoryChoice.OrderByRecentUse(All, expenses);

        Assert.Equal(["Transport", "Loisirs", "Alimentation", "Logement"], ordered.Select(x => x.Name));
    }

    [Fact]
    public void OrderByRecentUse_WithoutExpenses_SortsByName() =>
        Assert.Equal(["Alimentation", "Logement", "Loisirs", "Transport"], CategoryChoice.OrderByRecentUse(All.Reverse(), []).Select(x => x.Name));

    [Fact]
    public void OrderByRecentUse_OnlyLooksAtTheFiftyLatestExpenses()
    {
        var recent = Enumerable.Range(0, CategoryChoice.RecentExpensesConsidered)
            .Select(day => Samples.Expense(1, new DateTime(2026, 10, 1).AddDays(day), _food));
        var old = Samples.Expense(1, new DateTime(2026, 1, 1), _transport);

        var ordered = CategoryChoice.OrderByRecentUse(All, recent.Append(old));

        Assert.Equal(["Alimentation", "Logement", "Loisirs", "Transport"], ordered.Select(x => x.Name));
    }

    [Fact]
    public void Preferred_KeepsTheCurrentSelection()
    {
        var chosen = CategoryChoice.Preferred(All, _housing.Id, _leisure.Id.ToString(), _transport.Id.ToString(), rememberLast: true);

        Assert.Same(_housing, chosen);
    }

    [Fact]
    public void Preferred_UsesTheLastCategory_WhenRememberingIsOn()
    {
        var chosen = CategoryChoice.Preferred(All, null, _leisure.Id.ToString(), _transport.Id.ToString(), rememberLast: true);

        Assert.Same(_leisure, chosen);
    }

    [Fact]
    public void Preferred_UsesTheDefaultCategory_WhenRememberingIsOff()
    {
        var chosen = CategoryChoice.Preferred(All, null, _leisure.Id.ToString(), _transport.Id.ToString(), rememberLast: false);

        Assert.Same(_transport, chosen);
    }

    [Fact]
    public void Preferred_FallsBackToTheDefault_WhenTheLastCategoryNoLongerExists()
    {
        var chosen = CategoryChoice.Preferred(All, null, Guid.NewGuid().ToString(), _transport.Id.ToString(), rememberLast: true);

        Assert.Same(_transport, chosen);
    }

    [Fact]
    public void Preferred_FallsBackToTheFirstCategory_WhenNothingIsConfigured()
    {
        var chosen = CategoryChoice.Preferred(All, null, string.Empty, "pas-un-guid", rememberLast: true);

        Assert.Same(_food, chosen);
    }

    [Fact]
    public void Preferred_WithoutCategories_IsNull() =>
        Assert.Null(CategoryChoice.Preferred([], null, string.Empty, string.Empty, rememberLast: true));
}
