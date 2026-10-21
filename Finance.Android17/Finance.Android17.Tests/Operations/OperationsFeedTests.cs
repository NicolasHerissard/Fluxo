using Finance.Android17.Application;
using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Operations;

public class OperationsFeedTests
{
    [Fact]
    public void Recent_MixesIncomesAndExpenses_NewestFirst_WithSignedAmounts()
    {
        var expenses = new[] { Samples.Expense(1_250, new DateTime(2026, 10, 10), description: "Courses") };
        var incomes = new[]
        {
            Samples.Income(200_000, new DateTime(2026, 10, 1), "Salaire"),
            Samples.Income(5_000, new DateTime(2026, 10, 12), "Prime")
        };

        var feed = OperationsFeed.Recent(expenses, incomes);

        Assert.Equal(
        [
            new Operation(OperationKind.Income, "Prime", new DateTime(2026, 10, 12), 5_000),
            new Operation(OperationKind.Expense, "Courses", new DateTime(2026, 10, 10), -1_250),
            new Operation(OperationKind.Income, "Salaire", new DateTime(2026, 10, 1), 200_000)
        ], feed);
    }

    [Fact]
    public void Recent_BreaksTiesWithTheCreationTime()
    {
        var day = new DateTime(2026, 10, 10);
        var created = new DateTime(2026, 10, 10, 8, 0, 0, DateTimeKind.Utc);
        var expenses = new[] { Samples.Expense(100, day, description: "Plus tôt", createdAtUtc: created) };
        var incomes = new[] { Samples.Income(100, day, "Plus tard", createdAtUtc: created.AddMinutes(5)) };

        var feed = OperationsFeed.Recent(expenses, incomes);

        Assert.Equal(["Plus tard", "Plus tôt"], feed.Select(x => x.Description));
    }

    [Fact]
    public void Recent_KeepsOnlyTheRequestedCount_TwentyByDefault()
    {
        var expenses = Enumerable.Range(0, 30).Select(day => Samples.Expense(100, new DateTime(2026, 9, 1).AddDays(day))).ToList();

        Assert.Equal(OperationsFeed.DefaultCount, OperationsFeed.Recent(expenses, []).Count);
        Assert.Equal(20, OperationsFeed.DefaultCount);
        Assert.Equal(5, OperationsFeed.Recent(expenses, [], 5).Count);
        Assert.Equal(new DateTime(2026, 9, 30), OperationsFeed.Recent(expenses, [], 5)[0].OccurredOn);
    }

    [Fact]
    public void Recent_WithoutOperations_IsEmpty() =>
        Assert.Empty(OperationsFeed.Recent([], []));
}
