using Finance.Android17.Application;

namespace Finance.Android17.Tests.Recurring;

public sealed class RecurrenceTests
{
    [Fact]
    public void GetDueDates_ReturnsEveryMissedOccurrence_AndStopsAfterToday()
    {
        var dates = Recurrence.GetDueDates(new DateTime(2026, 6, 15), 1, new DateTime(2026, 9, 26));

        Assert.Equal([new DateTime(2026, 6, 15), new DateTime(2026, 7, 15), new DateTime(2026, 8, 15), new DateTime(2026, 9, 15)], dates);
    }

    [Fact]
    public void GetDueDates_UsesOneMonth_WhenIntervalIsInvalid()
    {
        var dates = Recurrence.GetDueDates(new DateTime(2026, 8, 1), 0, new DateTime(2026, 9, 26));

        Assert.Equal(2, dates.Count);
    }

    [Fact]
    public void GetDueDates_IncludesToday_AndIgnoresTheTimeOfDay()
    {
        var dates = Recurrence.GetDueDates(new DateTime(2026, 10, 20, 14, 0, 0), 1, new DateTime(2026, 10, 20, 8, 0, 0));

        Assert.Equal([new DateTime(2026, 10, 20)], dates);
    }

    [Fact]
    public void GetDueDates_IsEmpty_WhenTheNextDateIsInTheFuture()
    {
        Assert.Empty(Recurrence.GetDueDates(new DateTime(2026, 10, 21), 1, new DateTime(2026, 10, 20)));
    }

    [Theory]
    [InlineData(1, 2026, 11)]
    [InlineData(3, 2027, 1)]
    [InlineData(12, 2027, 10)]
    [InlineData(0, 2026, 11)]
    [InlineData(-4, 2026, 11)]
    public void NextAfter_AddsTheIntervalInMonths_WithAMinimumOfOne(int interval, int expectedYear, int expectedMonth)
    {
        Assert.Equal(new DateTime(expectedYear, expectedMonth, 15), Recurrence.NextAfter(new DateTime(2026, 10, 15), interval));
    }
}
