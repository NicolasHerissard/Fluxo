using Finance.Android17.Domain;

namespace Finance.Android17.Application;

/// <summary>Règles de rattrapage des échéances : quelles dépenses et quels revenus créer, et à quelle date repartir.</summary>
public static class DueOperationsPlanner
{
    public static DueOperationsPlan PlanSubscriptions(IEnumerable<Subscription> subscriptions, DateTime today, string fallbackCurrency)
    {
        var expenses = new List<Expense>();
        var advances = new List<DueAdvance>();
        foreach (var subscription in subscriptions.Where(x => x.DeletedAtUtc is null && x.NextBillingDate.Date <= today.Date))
        {
            var dueDates = Recurrence.GetDueDates(subscription.NextBillingDate, subscription.BillingIntervalMonths, today);
            foreach (var dueDate in dueDates)
            {
                expenses.Add(new Expense
                {
                    AmountMinor = subscription.AmountMinor,
                    CurrencyCode = CurrencyOrFallback(subscription.CurrencyCode, fallbackCurrency),
                    Description = $"Abonnement · {subscription.Name}",
                    OccurredOn = dueDate
                });
            }
            if (dueDates.Count > 0)
                advances.Add(new DueAdvance(subscription.Id, Recurrence.NextAfter(dueDates[^1], subscription.BillingIntervalMonths)));
        }
        return new DueOperationsPlan(expenses, advances, [], []);
    }

    public static DueOperationsPlan PlanRecurringIncomes(IEnumerable<RecurringIncome> recurringIncomes, DateTime today, string fallbackCurrency)
    {
        var incomes = new List<Income>();
        var advances = new List<DueAdvance>();
        foreach (var recurring in recurringIncomes.Where(x => x.DeletedAtUtc is null && x.NextOccurrenceDate.Date <= today.Date))
        {
            var dueDates = Recurrence.GetDueDates(recurring.NextOccurrenceDate, recurring.IntervalMonths, today);
            foreach (var dueDate in dueDates)
            {
                incomes.Add(new Income
                {
                    AmountMinor = recurring.AmountMinor,
                    CurrencyCode = CurrencyOrFallback(recurring.CurrencyCode, fallbackCurrency),
                    Description = recurring.Name,
                    OccurredOn = dueDate,
                    RecurringIncomeId = recurring.Id
                });
            }
            if (dueDates.Count > 0)
                advances.Add(new DueAdvance(recurring.Id, Recurrence.NextAfter(dueDates[^1], recurring.IntervalMonths)));
        }
        return new DueOperationsPlan([], [], incomes, advances);
    }

    public static DueOperationsPlan Combine(DueOperationsPlan subscriptions, DueOperationsPlan recurringIncomes) =>
        new(subscriptions.Expenses, subscriptions.SubscriptionAdvances, recurringIncomes.Incomes, recurringIncomes.RecurringIncomeAdvances);

    private static string CurrencyOrFallback(string currency, string fallback) => string.IsNullOrWhiteSpace(currency) ? fallback : currency;
}
