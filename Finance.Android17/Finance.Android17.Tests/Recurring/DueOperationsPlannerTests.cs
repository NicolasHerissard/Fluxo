using Finance.Android17.Application;
using Finance.Android17.Domain;

namespace Finance.Android17.Tests.Recurring;

public sealed class DueOperationsPlannerTests
{
    private static readonly DateTime Today = new(2026, 10, 20);

    private static Subscription Subscription(string name, DateTime next, int interval = 1, string currency = "EUR", long amount = 1_299) =>
        new() { Name = name, NextBillingDate = next, BillingIntervalMonths = interval, CurrencyCode = currency, AmountMinor = amount };

    [Fact]
    public void Subscriptions_ProduceOneExpensePerMissedDate_AndAdvanceToTheNextOne()
    {
        var netflix = Subscription("Netflix", new DateTime(2026, 8, 15));

        var plan = DueOperationsPlanner.PlanSubscriptions([netflix], Today, "EUR");

        Assert.Equal([new DateTime(2026, 8, 15), new DateTime(2026, 9, 15), new DateTime(2026, 10, 15)], plan.Expenses.Select(x => x.OccurredOn));
        Assert.All(plan.Expenses, expense =>
        {
            Assert.Equal("Abonnement · Netflix", expense.Description);
            Assert.Equal(1_299, expense.AmountMinor);
            Assert.Equal("EUR", expense.CurrencyCode);
        });
        Assert.Equal(new DueAdvance(netflix.Id, new DateTime(2026, 11, 15)), Assert.Single(plan.SubscriptionAdvances));
        Assert.Empty(plan.Incomes);
        Assert.Empty(plan.RecurringIncomeAdvances);
    }

    [Fact]
    public void Subscriptions_DueOnThe31st_KeepTheirDay_AfterAShortMonth()
    {
        var rent = Subscription("Loyer", new DateTime(2026, 1, 31));
        rent.AnchorDay = 31;

        var plan = DueOperationsPlanner.PlanSubscriptions([rent], new DateTime(2026, 3, 15), "EUR");

        Assert.Equal([new DateTime(2026, 1, 31), new DateTime(2026, 2, 28)], plan.Expenses.Select(x => x.OccurredOn));
        // Reprise un 31 mars, pas un 28.
        Assert.Equal(new DateTime(2026, 3, 31), Assert.Single(plan.SubscriptionAdvances).NextDate);
    }

    [Fact]
    public void RecurringIncomes_DueOnThe30th_KeepTheirDay_AfterFebruary()
    {
        var salary = new RecurringIncome { Name = "Salaire", AmountMinor = 200_000, NextOccurrenceDate = new DateTime(2027, 2, 28), AnchorDay = 30 };

        var plan = DueOperationsPlanner.PlanRecurringIncomes([salary], new DateTime(2027, 3, 1), "EUR");

        Assert.Equal([new DateTime(2027, 2, 28)], plan.Incomes.Select(x => x.OccurredOn));
        Assert.Equal(new DateTime(2027, 3, 30), Assert.Single(plan.RecurringIncomeAdvances).NextDate);
    }

    [Fact]
    public void Subscriptions_NotYetDue_OrDeleted_AreLeftAlone()
    {
        var future = Subscription("Assurance", new DateTime(2027, 1, 1), 12);
        var deleted = Subscription("Ancien", new DateTime(2026, 9, 1));
        deleted.DeletedAtUtc = DateTime.UtcNow;

        var plan = DueOperationsPlanner.PlanSubscriptions([future, deleted], Today, "EUR");

        Assert.True(plan.IsEmpty);
    }

    [Fact]
    public void Subscriptions_UseTheFallbackCurrency_WhenTheirOwnIsBlank()
    {
        var plan = DueOperationsPlanner.PlanSubscriptions([Subscription("Cloud", new DateTime(2026, 10, 1), currency: " ")], Today, "CHF");

        Assert.Equal("CHF", Assert.Single(plan.Expenses).CurrencyCode);
    }

    [Fact]
    public void Subscriptions_HonourTheirOwnIntervalWhenCatchingUp()
    {
        var plan = DueOperationsPlanner.PlanSubscriptions([Subscription("Journal", new DateTime(2026, 1, 10), interval: 3)], Today, "EUR");

        Assert.Equal([new DateTime(2026, 1, 10), new DateTime(2026, 4, 10), new DateTime(2026, 7, 10), new DateTime(2026, 10, 10)], plan.Expenses.Select(x => x.OccurredOn));
        Assert.Equal(new DateTime(2027, 1, 10), Assert.Single(plan.SubscriptionAdvances).NextDate);
    }

    [Fact]
    public void RecurringIncomes_ProduceLinkedIncomes_AndAdvance()
    {
        var salary = new RecurringIncome { Name = "Salaire", AmountMinor = 250_000, NextOccurrenceDate = new DateTime(2026, 9, 1), IntervalMonths = 1, CurrencyCode = "EUR" };

        var plan = DueOperationsPlanner.PlanRecurringIncomes([salary], Today, "EUR");

        Assert.Equal([new DateTime(2026, 9, 1), new DateTime(2026, 10, 1)], plan.Incomes.Select(x => x.OccurredOn));
        Assert.All(plan.Incomes, income =>
        {
            Assert.Equal(salary.Id, income.RecurringIncomeId);
            Assert.Equal("Salaire", income.Description);
            Assert.Equal(250_000, income.AmountMinor);
        });
        Assert.Equal(new DueAdvance(salary.Id, new DateTime(2026, 11, 1)), Assert.Single(plan.RecurringIncomeAdvances));
        Assert.Empty(plan.Expenses);
    }

    [Fact]
    public void Combine_KeepsTheExpensesOfOnePlan_AndTheIncomesOfTheOther()
    {
        var subscriptions = DueOperationsPlanner.PlanSubscriptions([Subscription("Netflix", new DateTime(2026, 10, 1))], Today, "EUR");
        var incomes = DueOperationsPlanner.PlanRecurringIncomes([new RecurringIncome { Name = "Salaire", NextOccurrenceDate = new DateTime(2026, 10, 1) }], Today, "EUR");

        var plan = DueOperationsPlanner.Combine(subscriptions, incomes);

        Assert.Single(plan.Expenses);
        Assert.Single(plan.SubscriptionAdvances);
        Assert.Single(plan.Incomes);
        Assert.Single(plan.RecurringIncomeAdvances);
        Assert.False(plan.IsEmpty);
        Assert.True(DueOperationsPlan.Empty.IsEmpty);
    }
}
