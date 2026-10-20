using Finance.Android17.Application;

namespace Finance.Android17.Tests.Dashboard;

public class PeriodRangeTests
{
    [Fact]
    public void Week_StartsOnMonday_AndComparesWithTheWholePreviousWeek()
    {
        var range = PeriodRange.For(DashboardPeriod.Week, new DateTime(2026, 10, 22, 14, 0, 0));

        Assert.Equal(new DateTime(2026, 10, 19), range.From);
        Assert.Equal(new DateTime(2026, 10, 22), range.To);
        Assert.Equal(new DateTime(2026, 10, 12), range.PreviousFrom);
        Assert.Equal(new DateTime(2026, 10, 18), range.PreviousTo);
    }

    [Theory]
    [InlineData(2026, 10, 19)] // lundi
    [InlineData(2026, 10, 25)] // dimanche
    public void Week_KeepsMondayAndSundayInTheSameWeek(int year, int month, int day)
    {
        var range = PeriodRange.For(DashboardPeriod.Week, new DateTime(year, month, day));

        Assert.Equal(new DateTime(2026, 10, 19), range.From);
    }

    [Fact]
    public void Month_RunsFromTheFirstToToday_AndComparesWithTheWholePreviousMonth()
    {
        var range = PeriodRange.For(DashboardPeriod.Month, new DateTime(2026, 3, 15));

        Assert.Equal(new DateTime(2026, 3, 1), range.From);
        Assert.Equal(new DateTime(2026, 3, 15), range.To);
        Assert.Equal(new DateTime(2026, 2, 1), range.PreviousFrom);
        Assert.Equal(new DateTime(2026, 2, 28), range.PreviousTo);
    }

    [Fact]
    public void Month_InJanuary_ComparesWithDecemberOfThePreviousYear()
    {
        var range = PeriodRange.For(DashboardPeriod.Month, new DateTime(2026, 1, 10));

        Assert.Equal(new DateTime(2025, 12, 1), range.PreviousFrom);
        Assert.Equal(new DateTime(2025, 12, 31), range.PreviousTo);
    }

    [Fact]
    public void Year_RunsFromJanuaryFirstToToday_AndComparesWithThePreviousYear()
    {
        var range = PeriodRange.For(DashboardPeriod.Year, new DateTime(2026, 10, 20));

        Assert.Equal(new DateTime(2026, 1, 1), range.From);
        Assert.Equal(new DateTime(2026, 10, 20), range.To);
        Assert.Equal(new DateTime(2025, 1, 1), range.PreviousFrom);
        Assert.Equal(new DateTime(2025, 12, 31), range.PreviousTo);
    }

    [Fact]
    public void Contains_IsInclusive_AndIgnoresTheTimeOfDay()
    {
        var range = PeriodRange.For(DashboardPeriod.Month, new DateTime(2026, 10, 20));

        Assert.True(range.Contains(new DateTime(2026, 10, 1, 0, 0, 0)));
        Assert.True(range.Contains(new DateTime(2026, 10, 20, 23, 59, 0)));
        Assert.False(range.Contains(new DateTime(2026, 9, 30, 23, 59, 0)));
        Assert.False(range.Contains(new DateTime(2026, 10, 21)));
        Assert.True(range.PreviousContains(new DateTime(2026, 9, 30, 12, 0, 0)));
        Assert.False(range.PreviousContains(new DateTime(2026, 10, 1)));
    }
}
