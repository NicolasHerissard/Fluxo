using Finance.Android17.Application;

namespace Finance.Android17.Tests.Dashboard;

public class PeriodComparisonTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(5_000, 0)]
    public void Between_HasNoReference_WhenThePreviousPeriodIsEmpty(long current, long previous)
    {
        var comparison = PeriodComparison.Between(current, previous);

        Assert.Equal(TrendDirection.NoReference, comparison.Direction);
        Assert.Equal(0, comparison.Percent);
    }

    [Fact]
    public void Between_ReportsAnIncrease() =>
        Assert.Equal(new PeriodComparison(TrendDirection.Up, 25), PeriodComparison.Between(12_500, 10_000));

    [Fact]
    public void Between_ReportsADecrease_AsAPositivePercent() =>
        Assert.Equal(new PeriodComparison(TrendDirection.Down, 40), PeriodComparison.Between(6_000, 10_000));

    [Fact]
    public void Between_ReportsNoChange() =>
        Assert.Equal(new PeriodComparison(TrendDirection.Same, 0), PeriodComparison.Between(10_000, 10_000));

    [Fact]
    public void Between_CanExceedOneHundredPercent() =>
        Assert.Equal(new PeriodComparison(TrendDirection.Up, 300), PeriodComparison.Between(40_000, 10_000));
}
