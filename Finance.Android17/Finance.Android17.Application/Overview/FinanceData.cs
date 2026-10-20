using Finance.Android17.Domain;

namespace Finance.Android17.Application;

/// <summary>Tout ce dont les calculs de synthèse ont besoin, lu en une fois.</summary>
public sealed record FinanceData(
    IReadOnlyList<Expense> Expenses,
    IReadOnlyList<Income> Incomes,
    IReadOnlyList<Subscription> Subscriptions,
    IReadOnlyList<RecurringIncome> RecurringIncomes,
    long InitialBalanceMinor);

public sealed class FinanceDataLoader(
    IExpenseRepository expenses,
    IIncomeRepository incomes,
    ISubscriptionRepository subscriptions,
    IRecurringIncomeRepository recurringIncomes,
    IFinancialProfileRepository profile)
{
    public async Task<FinanceData> LoadAsync(CancellationToken cancellationToken = default) =>
        new(await expenses.GetAllAsync(cancellationToken).ConfigureAwait(false),
            await incomes.GetAllAsync(cancellationToken).ConfigureAwait(false),
            await subscriptions.GetAllAsync(cancellationToken).ConfigureAwait(false),
            await recurringIncomes.GetAllAsync(cancellationToken).ConfigureAwait(false),
            await profile.GetInitialBalanceAsync(cancellationToken).ConfigureAwait(false));
}
