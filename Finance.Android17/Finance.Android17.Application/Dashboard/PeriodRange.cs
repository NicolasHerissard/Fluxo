namespace Finance.Android17.Application;

public enum DashboardPeriod { Week, Month, Year }

/// <summary>
/// Période de l'accueil et période de comparaison. La semaine commence le lundi ; la comparaison porte sur la
/// semaine, le mois ou l'année <b>précédents entiers</b> (pas sur la même durée écoulée).
/// </summary>
public sealed record PeriodRange(DateTime From, DateTime To, DateTime PreviousFrom, DateTime PreviousTo)
{
    public static PeriodRange For(DashboardPeriod period, DateTime today)
    {
        today = today.Date;
        switch (period)
        {
            case DashboardPeriod.Week:
                var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
                return new PeriodRange(monday, today, monday.AddDays(-7), monday.AddDays(-1));
            case DashboardPeriod.Year:
                var january = new DateTime(today.Year, 1, 1);
                return new PeriodRange(january, today, january.AddYears(-1), january.AddDays(-1));
            default:
                var firstOfMonth = new DateTime(today.Year, today.Month, 1);
                return new PeriodRange(firstOfMonth, today, firstOfMonth.AddMonths(-1), firstOfMonth.AddDays(-1));
        }
    }

    public bool Contains(DateTime date) => date.Date >= From && date.Date <= To;

    public bool PreviousContains(DateTime date) => date.Date >= PreviousFrom && date.Date <= PreviousTo;
}
