using Finance.Android17.Application;

namespace Finance.Android17.Tests.Expenses;

public class ExpensePeriodsTests
{
    private static readonly DateTime Today = new(2026, 10, 20, 9, 30, 0);
    private static readonly DateTime CustomFrom = new(2026, 3, 1, 12, 0, 0);
    private static readonly DateTime CustomTo = new(2026, 3, 15, 12, 0, 0);

    [Theory]
    [InlineData(ExpensePeriod.Today, "2026-10-20", "2026-10-20")]
    [InlineData(ExpensePeriod.Last7Days, "2026-10-14", "2026-10-20")]
    [InlineData(ExpensePeriod.ThisMonth, "2026-10-01", "2026-10-20")]
    [InlineData(ExpensePeriod.ThisYear, "2026-01-01", "2026-10-20")]
    [InlineData(ExpensePeriod.Custom, "2026-03-01", "2026-03-15")]
    public void Resolve_ReturnsInclusiveBounds(ExpensePeriod period, string from, string to)
    {
        var (resolvedFrom, resolvedTo) = ExpensePeriods.Resolve(period, Today, CustomFrom, CustomTo);

        Assert.Equal(DateTime.Parse(from), resolvedFrom);
        Assert.Equal(DateTime.Parse(to), resolvedTo);
    }

    [Fact]
    public void Resolve_AllTime_IsOpenEnded()
    {
        var (from, to) = ExpensePeriods.Resolve(ExpensePeriod.AllTime, Today, CustomFrom, CustomTo);

        Assert.Null(from);
        Assert.Null(to);
    }
}
