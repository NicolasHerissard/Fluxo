using Finance.Android17.Application;

namespace Finance.Android17.Tests.Dashboard;

public class BudgetStatusTests
{
    [Theory]
    [InlineData(10_000, 0, BudgetLevel.Ok)]
    [InlineData(10_000, 7_900, BudgetLevel.Ok)]
    [InlineData(10_000, 8_000, BudgetLevel.Warning)]
    [InlineData(10_000, 9_999, BudgetLevel.Warning)]
    [InlineData(10_000, 10_000, BudgetLevel.Exceeded)]
    [InlineData(10_000, 25_000, BudgetLevel.Exceeded)]
    public void Level_WarnsAtEightyPercent_AndAlertsAtOneHundred(long budget, long spent, BudgetLevel expected) =>
        Assert.Equal(expected, new BudgetStatus(budget, spent).Level);

    [Fact]
    public void Progress_IsCappedAtOne_AndRemainingGoesNegative()
    {
        var status = new BudgetStatus(10_000, 15_000);

        Assert.Equal(1, status.Progress);
        Assert.Equal(-5_000, status.RemainingMinor);
    }

    [Fact]
    public void Progress_IsZero_WhenThereIsNoBudget()
    {
        var status = new BudgetStatus(0, 4_000);

        Assert.Equal(0, status.Progress);
        Assert.Equal(BudgetLevel.Ok, status.Level);
    }
}
