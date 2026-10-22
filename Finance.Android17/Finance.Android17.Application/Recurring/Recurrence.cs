namespace Finance.Android17.Application;

/// <summary>Calcul des échéances d'un abonnement ou d'un revenu récurrent.</summary>
public static class Recurrence
{
    /// <summary>
    /// Toutes les échéances de <paramref name="nextDate"/> à <paramref name="today"/> incluses, une par intervalle.
    /// <paramref name="anchorDay"/> est le jour du mois voulu (31 pour « fin de mois ») ; 0 ou moins : celui de <paramref name="nextDate"/>.
    /// </summary>
    public static IReadOnlyList<DateTime> GetDueDates(DateTime nextDate, int intervalMonths, DateTime today, int anchorDay = 0)
    {
        var result = new List<DateTime>();
        var interval = Math.Max(1, intervalMonths);
        var anchor = anchorDay > 0 ? anchorDay : nextDate.Day;
        var date = nextDate.Date;
        while (date <= today.Date)
        {
            result.Add(date);
            date = NextAfter(date, interval, anchor);
        }
        return result;
    }

    /// <summary>
    /// Prochaine échéance après la dernière échéance traitée. Le jour d'ancrage est réappliqué à chaque mois : un 31
    /// ramené au 28 février redevient 31 mars, il ne reste pas bloqué au 28.
    /// </summary>
    public static DateTime NextAfter(DateTime lastDue, int intervalMonths, int anchorDay = 0)
    {
        var firstOfTarget = new DateTime(lastDue.Year, lastDue.Month, 1).AddMonths(Math.Max(1, intervalMonths));
        var day = Math.Min(anchorDay > 0 ? anchorDay : lastDue.Day, DateTime.DaysInMonth(firstOfTarget.Year, firstOfTarget.Month));
        return firstOfTarget.AddDays(day - 1);
    }
}
