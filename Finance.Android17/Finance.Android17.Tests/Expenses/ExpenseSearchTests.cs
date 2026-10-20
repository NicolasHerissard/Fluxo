using Finance.Android17.Application;
using Finance.Android17.Domain;
using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Expenses;

public class ExpenseSearchTests
{
    private readonly Category _food = Samples.Category("Alimentation");
    private readonly Category _transport = Samples.Category("Transport");
    private readonly List<Expense> _expenses;

    public ExpenseSearchTests()
    {
        _expenses =
        [
            Samples.Expense(350, new DateTime(2026, 10, 2), _food, "Café crème"),
            Samples.Expense(1_250, new DateTime(2026, 10, 10), _food, "Pain"),
            Samples.Expense(4_000, new DateTime(2026, 9, 15), _transport, "Train"),
            Samples.Expense(80_000, new DateTime(2026, 10, 10), null, "Loyer", new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc))
        ];
    }

    private IEnumerable<string> Descriptions(ExpenseCriteria criteria) =>
        ExpenseSearch.Apply(_expenses, criteria).Select(x => x.Description);

    [Fact]
    public void NoCriteria_KeepsEverything_NewestFirst() =>
        Assert.Equal(["Loyer", "Pain", "Café crème", "Train"], Descriptions(new ExpenseCriteria()));

    [Theory]
    [InlineData("cafe", new[] { "Café crème" })]
    [InlineData("CAFÉ", new[] { "Café crème" })]
    [InlineData("alimentation", new[] { "Pain", "Café crème" })]
    [InlineData("pain alimentation", new[] { "Pain" })]
    [InlineData("pain transport", new string[0])]
    [InlineData("12,50", new[] { "Pain" })]
    [InlineData("1250", new[] { "Pain" })]
    [InlineData("02/10/2026", new[] { "Café crème" })]
    [InlineData("15 septembre", new[] { "Train" })]
    [InlineData("   ", new[] { "Loyer", "Pain", "Café crème", "Train" })]
    public void SearchText_IgnoresCaseAndAccents_AndRequiresEveryWord(string text, string[] expected) =>
        Assert.Equal(expected, Descriptions(new ExpenseCriteria(SearchText: text)));

    [Fact]
    public void Categories_FilterOnTheCategoryId()
    {
        var criteria = new ExpenseCriteria(CategoryIds: new HashSet<Guid> { _transport.Id });

        Assert.Equal(["Train"], Descriptions(criteria));
    }

    [Fact]
    public void EmptyCategorySet_ExcludesNothing() =>
        Assert.Equal(4, ExpenseSearch.Count(_expenses, new ExpenseCriteria(CategoryIds: new HashSet<Guid>())));

    [Fact]
    public void DateRange_IsInclusive_AndIgnoresTheTimeOfDay()
    {
        var criteria = new ExpenseCriteria(From: new DateTime(2026, 10, 2, 18, 0, 0), To: new DateTime(2026, 10, 10, 6, 0, 0));

        Assert.Equal(["Loyer", "Pain", "Café crème"], Descriptions(criteria));
    }

    [Fact]
    public void AmountRange_IsInclusive()
    {
        var criteria = new ExpenseCriteria(MinimumMinor: 1_250, MaximumMinor: 4_000);

        Assert.Equal(["Pain", "Train"], Descriptions(criteria).OrderBy(x => x));
    }

    [Fact]
    public void Count_MatchesFilter()
    {
        var criteria = new ExpenseCriteria(From: new DateTime(2026, 10, 1));

        Assert.Equal(ExpenseSearch.Filter(_expenses, criteria).Count, ExpenseSearch.Count(_expenses, criteria));
        Assert.Equal(3, ExpenseSearch.Count(_expenses, criteria));
    }

    [Theory]
    [InlineData(ExpenseSort.Newest, new[] { "Loyer", "Pain", "Café crème", "Train" })]
    [InlineData(ExpenseSort.Oldest, new[] { "Train", "Café crème", "Pain", "Loyer" })]
    [InlineData(ExpenseSort.AmountDescending, new[] { "Loyer", "Train", "Pain", "Café crème" })]
    [InlineData(ExpenseSort.AmountAscending, new[] { "Café crème", "Pain", "Train", "Loyer" })]
    [InlineData(ExpenseSort.NameAscending, new[] { "Café crème", "Loyer", "Pain", "Train" })]
    [InlineData(ExpenseSort.NameDescending, new[] { "Train", "Pain", "Loyer", "Café crème" })]
    public void Sort_OrdersTheResult(ExpenseSort sort, string[] expected)
    {
        var expenses = new List<Expense>
        {
            Expense("Pain", 1_250, new DateTime(2026, 10, 10), 1),
            Expense("Loyer", 80_000, new DateTime(2026, 10, 10), 2),
            Expense("Café crème", 350, new DateTime(2026, 10, 2), 1),
            Expense("Train", 4_000, new DateTime(2026, 9, 15), 1)
        };

        Assert.Equal(expected, ExpenseSearch.Apply(expenses, new ExpenseCriteria(Sort: sort)).Select(x => x.Description));
    }

    [Fact]
    public void NameSort_FollowsFrenchAlphabeticalOrder()
    {
        var expenses = new[]
        {
            Samples.Expense(1, Fixed, description: "Zèbre"),
            Samples.Expense(1, Fixed, description: "Éclair"),
            Samples.Expense(1, Fixed, description: "banane")
        };

        var sorted = ExpenseSearch.Apply(expenses, new ExpenseCriteria(Sort: ExpenseSort.NameAscending));

        Assert.Equal(["banane", "Éclair", "Zèbre"], sorted.Select(x => x.Description));
    }

    private static readonly DateTime Fixed = new(2026, 10, 1);

    private static Expense Expense(string description, long amount, DateTime date, int createdHour) =>
        Samples.Expense(amount, date, description: description, createdAtUtc: new DateTime(2026, 1, 1, createdHour, 0, 0, DateTimeKind.Utc));
}
