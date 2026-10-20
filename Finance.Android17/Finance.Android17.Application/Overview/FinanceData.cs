using Finance.Android17.Domain;

namespace Finance.Android17.Application;

/// <summary>Tout ce dont les calculs de synthèse ont besoin, lu en une fois.</summary>
public sealed record FinanceData(
    IReadOnlyList<Expense> Expenses,
    IReadOnlyList<Income> Incomes,
    IReadOnlyList<Subscription> Subscriptions,
    IReadOnlyList<RecurringIncome> RecurringIncomes,
    long InitialBalanceMinor);

public sealed class FinanceDataLoader(IFinanceStore store)
{
    public async Task<FinanceData> LoadAsync(CancellationToken cancellationToken = default)
    {
        var expenses = await store.GetExpensesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var incomes = await store.GetIncomesAsync(cancellationToken).ConfigureAwait(false);
        var subscriptions = await store.GetSubscriptionsAsync(cancellationToken).ConfigureAwait(false);
        var recurringIncomes = await store.GetRecurringIncomesAsync(cancellationToken).ConfigureAwait(false);
        var initialBalance = await store.GetInitialBalanceAsync(cancellationToken).ConfigureAwait(false);
        return new FinanceData(expenses, incomes, subscriptions, recurringIncomes, initialBalance);
    }
}
