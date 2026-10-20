namespace Finance.Android17.Application;

/// <summary>Calcul des échéances d'un abonnement ou d'un revenu récurrent.</summary>
public static class Recurrence
{
    /// <summary>Toutes les échéances de <paramref name="nextDate"/> à <paramref name="today"/> incluses, une par intervalle.</summary>
    public static IReadOnlyList<DateTime> GetDueDates(DateTime nextDate, int intervalMonths, DateTime today)
    {
        var result = new List<DateTime>();
        var interval = Math.Max(1, intervalMonths);
        var date = nextDate.Date;
        while (date <= today.Date)
        {
            result.Add(date);
            date = date.AddMonths(interval);
        }
        return result;
    }

    /// <summary>Prochaine échéance après la dernière échéance traitée.</summary>
    public static DateTime NextAfter(DateTime lastDue, int intervalMonths) => lastDue.AddMonths(Math.Max(1, intervalMonths));
}
