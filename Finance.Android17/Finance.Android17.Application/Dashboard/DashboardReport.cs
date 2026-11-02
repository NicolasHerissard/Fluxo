using Finance.Android17.Domain;

namespace Finance.Android17.Application;

/// <summary>Dépenses d'une catégorie sur la période, avec sa part du total et l'état de son budget.</summary>
public sealed record CategorySpend(string Name, string Color, long TotalMinor, long? BudgetMinor, double Share)
{
    public BudgetStatus? Budget => BudgetMinor is > 0 ? new BudgetStatus(BudgetMinor.Value, TotalMinor) : null;
}

public sealed record DashboardReport(
    PeriodRange Range,
    long TotalMinor,
    int ExpenseCount,
    PeriodComparison Comparison,
    IReadOnlyList<CategorySpend> Categories,
    IReadOnlyList<Expense> RecentExpenses,
    Subscription? NextSubscription,
    BudgetStatus? MonthlyBudget,
    FinancialSummary Financial,
    IReadOnlyList<MonthlyFlow> Cashflow)
{
    public string? TopCategoryName => Categories.Count > 0 ? Categories[0].Name : null;
}
