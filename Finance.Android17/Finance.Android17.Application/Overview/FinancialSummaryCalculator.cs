namespace Finance.Android17.Application;

public static class FinancialSummaryCalculator
{
    /// <remarks>
    /// Le solde actuel cumule le solde initial et <b>toutes</b> les opérations enregistrées, y compris celles datées
    /// d'après <paramref name="today"/>. La prévision de fin de mois y ajoute les revenus récurrents et en retire les
    /// abonnements dont la prochaine échéance tombe après aujourd'hui et au plus tard le dernier jour du mois.
    /// </remarks>
    public static FinancialSummary Build(FinanceData data, DateTime today)
    {
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        bool InMonth(DateTime date) => date.Date >= monthStart && date.Date <= monthEnd;
        bool RestOfMonth(DateTime date) => date.Date > today.Date && date.Date <= monthEnd;

        var monthExpenses = data.Expenses.Where(x => InMonth(x.OccurredOn)).Sum(x => x.AmountMinor);
        var monthIncomes = data.Incomes.Where(x => InMonth(x.OccurredOn)).Sum(x => x.AmountMinor);
        var balance = data.InitialBalanceMinor + data.Incomes.Sum(x => x.AmountMinor) - data.Expenses.Sum(x => x.AmountMinor);
        var upcomingExpenses = data.Subscriptions.Where(x => x.DeletedAtUtc is null && RestOfMonth(x.NextBillingDate)).Sum(x => x.AmountMinor);
        var upcomingIncomes = data.RecurringIncomes.Where(x => x.DeletedAtUtc is null && RestOfMonth(x.NextOccurrenceDate)).Sum(x => x.AmountMinor);
        var net = monthIncomes - monthExpenses;
        var rate = monthIncomes <= 0 ? 0 : Math.Clamp((double)net / monthIncomes, -1, 1);
        return new FinancialSummary(monthExpenses, monthIncomes, net, balance, balance + upcomingIncomes - upcomingExpenses, rate);
    }
}
