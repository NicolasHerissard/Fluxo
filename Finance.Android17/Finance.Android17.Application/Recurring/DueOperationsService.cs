namespace Finance.Android17.Application;

/// <summary>Rattrape au déverrouillage les abonnements et revenus récurrents échus, selon les réglages de l'utilisateur.</summary>
public sealed class DueOperationsService(
    ISubscriptionRepository subscriptionRepository,
    IRecurringIncomeRepository recurringIncomeRepository,
    IDueOperationsStore store,
    ISettingsService settings,
    TimeProvider clock)
{
    public async Task<DueOperationsResult> ApplyAsync(CancellationToken cancellationToken = default)
    {
        var today = clock.GetLocalNow().Date;
        var currency = settings.CurrencyCode;
        var subscriptions = DueOperationsPlan.Empty;
        var recurringIncomes = DueOperationsPlan.Empty;
        if (settings.AutoCreateSubscriptionExpenses)
            subscriptions = DueOperationsPlanner.PlanSubscriptions(await subscriptionRepository.GetAllAsync(cancellationToken).ConfigureAwait(false), today, currency);
        if (settings.AutoCreateRecurringIncomes)
            recurringIncomes = DueOperationsPlanner.PlanRecurringIncomes(await recurringIncomeRepository.GetAllAsync(cancellationToken).ConfigureAwait(false), today, currency);

        var plan = DueOperationsPlanner.Combine(subscriptions, recurringIncomes);
        if (plan.IsEmpty) return new DueOperationsResult(0, 0);
        await store.ApplyAsync(plan, cancellationToken).ConfigureAwait(false);
        return new DueOperationsResult(plan.Expenses.Count, plan.Incomes.Count);
    }
}
