using Finance.Android17.Domain;

namespace Finance.Android17.Application;

public sealed class FinanceService(IFinanceStore store)
{
    public Task InitializeAsync(CancellationToken cancellationToken = default) => store.InitializeAsync(cancellationToken);
    public Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default) => store.GetCategoriesAsync(cancellationToken);
    public Task<IReadOnlyList<Expense>> GetExpensesAsync(ExpenseFilter? filter = null, CancellationToken cancellationToken = default) => store.GetExpensesAsync(filter, cancellationToken);
    public Task AddExpenseAsync(long amountMinor, string description, DateTime occurredOn, Guid categoryId, string currencyCode = "EUR", CancellationToken cancellationToken = default) =>
        store.AddExpenseAsync(amountMinor, description, occurredOn, categoryId, currencyCode, cancellationToken);
    public Task UpdateExpenseAsync(Guid id, long amountMinor, string description, DateTime occurredOn, Guid categoryId, string currencyCode = "EUR", CancellationToken cancellationToken = default) =>
        store.UpdateExpenseAsync(id, amountMinor, description, occurredOn, categoryId, currencyCode, cancellationToken);
    public Task DeleteExpenseAsync(Guid id, CancellationToken cancellationToken = default) => store.SoftDeleteExpenseAsync(id, cancellationToken);
    public Task RestoreExpenseAsync(Guid id, CancellationToken cancellationToken = default) => store.RestoreExpenseAsync(id, cancellationToken);
    public Task<IReadOnlyList<Income>> GetIncomesAsync(CancellationToken cancellationToken = default) => store.GetIncomesAsync(cancellationToken);
    public Task AddIncomeAsync(long amountMinor, string description, DateTime occurredOn, string currencyCode = "EUR", CancellationToken cancellationToken = default) =>
        store.AddIncomeAsync(amountMinor, description, occurredOn, currencyCode, cancellationToken);
    public Task UpdateIncomeAsync(Guid id, long amountMinor, string description, DateTime occurredOn, string currencyCode = "EUR", CancellationToken cancellationToken = default) =>
        store.UpdateIncomeAsync(id, amountMinor, description, occurredOn, currencyCode, cancellationToken);
    public Task DeleteIncomeAsync(Guid id, CancellationToken cancellationToken = default) => store.SoftDeleteIncomeAsync(id, cancellationToken);
    public Task<IReadOnlyList<RecurringIncome>> GetRecurringIncomesAsync(CancellationToken cancellationToken = default) => store.GetRecurringIncomesAsync(cancellationToken);
    public Task AddRecurringIncomeAsync(string name, long amountMinor, int intervalMonths, DateTime nextOccurrenceDate, string currencyCode = "EUR", CancellationToken cancellationToken = default) =>
        store.AddRecurringIncomeAsync(name, amountMinor, intervalMonths, nextOccurrenceDate, currencyCode, cancellationToken);
    public Task UpdateRecurringIncomeAsync(Guid id, string name, long amountMinor, int intervalMonths, DateTime nextOccurrenceDate, string currencyCode = "EUR", CancellationToken cancellationToken = default) =>
        store.UpdateRecurringIncomeAsync(id, name, amountMinor, intervalMonths, nextOccurrenceDate, currencyCode, cancellationToken);
    public Task DeleteRecurringIncomeAsync(Guid id, CancellationToken cancellationToken = default) => store.SoftDeleteRecurringIncomeAsync(id, cancellationToken);
    public Task<int> ProcessDueRecurringIncomesAsync(DateTime today, string currencyCode, CancellationToken cancellationToken = default) => store.ProcessDueRecurringIncomesAsync(today, currencyCode, cancellationToken);
    public Task<long> GetInitialBalanceAsync(CancellationToken cancellationToken = default) => store.GetInitialBalanceAsync(cancellationToken);
    public Task SetInitialBalanceAsync(long amountMinor, CancellationToken cancellationToken = default) => store.SetInitialBalanceAsync(amountMinor, cancellationToken);
    public Task<IReadOnlyList<SavingsGoal>> GetSavingsGoalsAsync(CancellationToken cancellationToken = default) => store.GetSavingsGoalsAsync(cancellationToken);
    public Task AddSavingsGoalAsync(string name, long targetAmountMinor, long savedAmountMinor, DateTime? targetDate, CancellationToken cancellationToken = default) =>
        store.AddSavingsGoalAsync(name, targetAmountMinor, savedAmountMinor, targetDate, cancellationToken);
    public Task UpdateSavingsGoalAsync(Guid id, string name, long targetAmountMinor, long savedAmountMinor, DateTime? targetDate, CancellationToken cancellationToken = default) =>
        store.UpdateSavingsGoalAsync(id, name, targetAmountMinor, savedAmountMinor, targetDate, cancellationToken);
    public Task DeleteSavingsGoalAsync(Guid id, CancellationToken cancellationToken = default) => store.SoftDeleteSavingsGoalAsync(id, cancellationToken);
    public Task AddCategoryAsync(string name, string color, long? budgetMinor = null, CancellationToken cancellationToken = default) => store.AddCategoryAsync(name, color, budgetMinor, cancellationToken);
    public Task UpdateCategoryAsync(Guid id, string name, string color, long? budgetMinor, CancellationToken cancellationToken = default) => store.UpdateCategoryAsync(id, name, color, budgetMinor, cancellationToken);
    public Task DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default) => store.SoftDeleteCategoryAsync(id, cancellationToken);
    public Task<IReadOnlyList<Subscription>> GetSubscriptionsAsync(CancellationToken cancellationToken = default) => store.GetSubscriptionsAsync(cancellationToken);
    public Task AddSubscriptionAsync(string name, long amountMinor, int intervalMonths, DateTime nextDate, string currencyCode = "EUR", CancellationToken cancellationToken = default) =>
        store.AddSubscriptionAsync(name, amountMinor, intervalMonths, nextDate, currencyCode, cancellationToken);
    public Task UpdateSubscriptionAsync(Guid id, string name, long amountMinor, int intervalMonths, DateTime nextDate, string currencyCode = "EUR", CancellationToken cancellationToken = default) =>
        store.UpdateSubscriptionAsync(id, name, amountMinor, intervalMonths, nextDate, currencyCode, cancellationToken);
    public Task DeleteSubscriptionAsync(Guid id, CancellationToken cancellationToken = default) => store.SoftDeleteSubscriptionAsync(id, cancellationToken);
    public Task<int> ProcessDueSubscriptionsAsync(DateTime today, string currencyCode, CancellationToken cancellationToken = default) => store.ProcessDueSubscriptionsAsync(today, currencyCode, cancellationToken);
    public Task<FinanceBackupSnapshot> CreateBackupSnapshotAsync(CancellationToken cancellationToken = default) => store.CreateBackupSnapshotAsync(cancellationToken);
    public Task RestoreBackupSnapshotAsync(FinanceBackupSnapshot snapshot, CancellationToken cancellationToken = default) => store.RestoreBackupSnapshotAsync(snapshot, cancellationToken);
    public Task DeleteAllDataAsync(CancellationToken cancellationToken = default) => store.DeleteAllDataAsync(cancellationToken);

    public static DashboardSummary BuildDashboard(IEnumerable<Expense> expenses, DateTime today)
    {
        var monthly = expenses.Where(x => x.OccurredOn.Year == today.Year && x.OccurredOn.Month == today.Month).ToList();
        var top = monthly.GroupBy(x => x.Category?.Name ?? "Sans catégorie")
            .OrderByDescending(x => x.Sum(y => y.AmountMinor)).Select(x => x.Key).FirstOrDefault() ?? "—";
        return new(monthly.Sum(x => x.AmountMinor), monthly.Count, top);
    }

    public static IReadOnlyList<DateTime> GetDueDates(DateTime nextBillingDate, int intervalMonths, DateTime today)
    {
        var result = new List<DateTime>();
        var interval = Math.Max(1, intervalMonths);
        var date = nextBillingDate.Date;
        while (date <= today.Date)
        {
            result.Add(date);
            date = date.AddMonths(interval);
        }
        return result;
    }

    public static FinancialSummary BuildFinancialSummary(
        IEnumerable<Expense> expenses,
        IEnumerable<Income> incomes,
        long initialBalanceMinor,
        IEnumerable<Subscription> subscriptions,
        IEnumerable<RecurringIncome> recurringIncomes,
        DateTime today)
    {
        var expenseList = expenses.ToList();
        var incomeList = incomes.ToList();
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var monthExpenses = expenseList.Where(x => x.OccurredOn.Date >= monthStart && x.OccurredOn.Date <= monthEnd).Sum(x => x.AmountMinor);
        var monthIncomes = incomeList.Where(x => x.OccurredOn.Date >= monthStart && x.OccurredOn.Date <= monthEnd).Sum(x => x.AmountMinor);
        var balance = initialBalanceMinor + incomeList.Sum(x => x.AmountMinor) - expenseList.Sum(x => x.AmountMinor);
        var upcomingExpenses = subscriptions
            .Where(x => x.DeletedAtUtc is null && x.NextBillingDate.Date > today.Date && x.NextBillingDate.Date <= monthEnd)
            .Sum(x => x.AmountMinor);
        var upcomingIncomes = recurringIncomes
            .Where(x => x.DeletedAtUtc is null && x.NextOccurrenceDate.Date > today.Date && x.NextOccurrenceDate.Date <= monthEnd)
            .Sum(x => x.AmountMinor);
        var net = monthIncomes - monthExpenses;
        var rate = monthIncomes <= 0 ? 0 : Math.Clamp((double)net / monthIncomes, -1, 1);
        return new FinancialSummary(monthExpenses, monthIncomes, net, balance, balance + upcomingIncomes - upcomingExpenses, rate);
    }
}
