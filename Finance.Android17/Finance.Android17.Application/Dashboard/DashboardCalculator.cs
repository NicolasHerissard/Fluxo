namespace Finance.Android17.Application;

public static class DashboardCalculator
{
    public const int RecentExpenseCount = 3;

    /// <param name="monthlyBudgetMinor">Budget mensuel global ; ignoré (aucun suivi) hors période « mois » ou s'il vaut 0.</param>
    public static DashboardReport Build(FinanceData data, DashboardPeriod period, DateTime today, long monthlyBudgetMinor)
    {
        var range = PeriodRange.For(period, today);
        var current = data.Expenses.Where(x => range.Contains(x.OccurredOn)).ToList();
        var total = current.Sum(x => x.AmountMinor);
        var previousTotal = data.Expenses.Where(x => range.PreviousContains(x.OccurredOn)).Sum(x => x.AmountMinor);
        var distributionTotal = Math.Max(1, total);

        var categories = current
            .GroupBy(x => x.Category?.Id ?? Guid.Empty)
            .Select(g =>
            {
                var category = g.First().Category;
                var spent = g.Sum(x => x.AmountMinor);
                return new CategorySpend(category?.Name ?? Uncategorized.Name, category?.Color ?? Uncategorized.Color, spent, category?.BudgetMinor, (double)spent / distributionTotal);
            })
            .OrderByDescending(x => x.TotalMinor)
            .ToList();

        var recent = data.Expenses.OrderByDescending(x => x.OccurredOn).ThenByDescending(x => x.CreatedAtUtc).Take(RecentExpenseCount).ToList();
        var next = data.Subscriptions.Where(x => x.NextBillingDate.Date >= today.Date).OrderBy(x => x.NextBillingDate).FirstOrDefault();
        var budget = period == DashboardPeriod.Month && monthlyBudgetMinor > 0 ? new BudgetStatus(monthlyBudgetMinor, total) : null;

        return new DashboardReport(
            range, total, current.Count, PeriodComparison.Between(total, previousTotal),
            categories, recent, next, budget, FinancialSummaryCalculator.Build(data, today), CashflowCalculator.Build(data, today));
    }
}
