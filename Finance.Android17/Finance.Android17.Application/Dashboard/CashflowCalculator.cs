namespace Finance.Android17.Application;

/// <summary>Revenus et dépenses d'un mois civil (<see cref="Month"/> est son premier jour).</summary>
public sealed record MonthlyFlow(DateTime Month, long ExpenseMinor, long IncomeMinor);

/// <summary>Flux de trésorerie des derniers mois, pour le graphique de l'accueil.</summary>
public static class CashflowCalculator
{
    public const int DefaultMonths = 6;

    /// <returns>Les <paramref name="months"/> derniers mois, du plus ancien au mois en cours inclus.</returns>
    public static IReadOnlyList<MonthlyFlow> Build(FinanceData data, DateTime today, int months = DefaultMonths)
    {
        var first = new DateTime(today.Year, today.Month, 1).AddMonths(1 - months);
        var flows = new List<MonthlyFlow>(months);
        for (var i = 0; i < months; i++)
        {
            var start = first.AddMonths(i);
            var end = start.AddMonths(1);
            flows.Add(new MonthlyFlow(
                start,
                data.Expenses.Where(x => x.OccurredOn.Date >= start && x.OccurredOn.Date < end).Sum(x => x.AmountMinor),
                data.Incomes.Where(x => x.OccurredOn.Date >= start && x.OccurredOn.Date < end).Sum(x => x.AmountMinor)));
        }
        return flows;
    }
}
