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

    [Fact]
    public void GetDueDates_KeepsTheEndOfMonthDay_InsteadOfDriftingTo28()
    {
        var dates = Recurrence.GetDueDates(new DateTime(2026, 1, 31), 1, new DateTime(2026, 5, 31));

        Assert.Equal([new DateTime(2026, 1, 31), new DateTime(2026, 2, 28), new DateTime(2026, 3, 31), new DateTime(2026, 4, 30), new DateTime(2026, 5, 31)], dates);
    }

    [Fact]
    public void GetDueDates_UsesTheAnchorDay_WhenTheNextDateWasAlreadyClamped()
    {
        // 28 février portait l'ancre 31 : l'échéance suivante est bien le 31 mars.
        var dates = Recurrence.GetDueDates(new DateTime(2026, 2, 28), 1, new DateTime(2026, 4, 30), anchorDay: 31);

        Assert.Equal([new DateTime(2026, 2, 28), new DateTime(2026, 3, 31), new DateTime(2026, 4, 30)], dates);
    }

    [Fact]
    public void NextAfter_RestoresTheAnchorDay_AfterAShortMonth()
    {
        Assert.Equal(new DateTime(2026, 3, 31), Recurrence.NextAfter(new DateTime(2026, 2, 28), 1, anchorDay: 31));
        Assert.Equal(new DateTime(2028, 2, 29), Recurrence.NextAfter(new DateTime(2027, 2, 28), 12, anchorDay: 29));
        Assert.Equal(new DateTime(2026, 12, 31), Recurrence.NextAfter(new DateTime(2026, 9, 30), 3, anchorDay: 31));
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
