using Finance.Android17.Application;
using Finance.Android17.Domain;
using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Overview;

public class FinancialSummaryCalculatorTests
{
    private static FinanceData Data(
        IEnumerable<Expense>? expenses = null,
        IEnumerable<Income>? incomes = null,
        IEnumerable<Subscription>? subscriptions = null,
        IEnumerable<RecurringIncome>? recurringIncomes = null,
        long initialBalance = 0) =>
        new([.. expenses ?? []], [.. incomes ?? []], [.. subscriptions ?? []], [.. recurringIncomes ?? []], initialBalance);

    [Fact]
    public void Build_ComputesBalanceNetAndForecast()
    {
        var data = Data(
            expenses: [Samples.Expense(40_000, new DateTime(2026, 10, 2)), Samples.Expense(10_000, new DateTime(2026, 9, 2))],
            incomes: [Samples.Income(250_000, new DateTime(2026, 10, 1))],
            subscriptions: [Samples.Subscription(12_000, new DateTime(2026, 10, 15))],
            recurringIncomes: [Samples.RecurringIncome(30_000, new DateTime(2026, 10, 20))],
            initialBalance: 100_000);

        var result = FinancialSummaryCalculator.Build(data, new DateTime(2026, 10, 3));

        Assert.Equal(40_000, result.ExpenseTotalMinor);
        Assert.Equal(250_000, result.IncomeTotalMinor);
        Assert.Equal(210_000, result.NetTotalMinor);
        Assert.Equal(300_000, result.CurrentBalanceMinor);
        Assert.Equal(318_000, result.ForecastBalanceMinor);
        Assert.Equal(0.84, result.SavingsRate, 2);
    }

    [Fact]
    public void Build_DoesNotDivideByZeroWithoutIncome()
    {
        var result = FinancialSummaryCalculator.Build(Data(initialBalance: 12_500), new DateTime(2026, 10, 3));

        Assert.Equal(12_500, result.CurrentBalanceMinor);
        Assert.Equal(0, result.SavingsRate);
    }

    [Fact]
    public void Build_ClampsTheSavingsRate_WhenSpendingExceedsIncome()
    {
        var data = Data(
            expenses: [Samples.Expense(90_000, new DateTime(2026, 10, 2))],
            incomes: [Samples.Income(10_000, new DateTime(2026, 10, 1))]);

        var result = FinancialSummaryCalculator.Build(data, new DateTime(2026, 10, 3));

        Assert.Equal(-80_000, result.NetTotalMinor);
        Assert.Equal(-1, result.SavingsRate);
    }

    [Fact]
    public void Build_CountsTheWholeMonth_IncludingFirstAndLastDay()
    {
        var data = Data(
            expenses:
            [
                Samples.Expense(100, new DateTime(2026, 9, 30)),
                Samples.Expense(200, new DateTime(2026, 10, 1)),
                Samples.Expense(400, new DateTime(2026, 10, 31)),
                Samples.Expense(800, new DateTime(2026, 11, 1))
            ]);

        var result = FinancialSummaryCalculator.Build(data, new DateTime(2026, 10, 15));

        Assert.Equal(600, result.ExpenseTotalMinor);
    }

    [Fact]
    public void Build_BalanceIncludesOperationsDatedAfterToday()
    {
        var data = Data(
            expenses: [Samples.Expense(1_000, new DateTime(2027, 3, 1))],
            incomes: [Samples.Income(5_000, new DateTime(2026, 12, 1))],
            initialBalance: 2_000);

        var result = FinancialSummaryCalculator.Build(data, new DateTime(2026, 10, 3));

        Assert.Equal(6_000, result.CurrentBalanceMinor);
        Assert.Equal(0, result.ExpenseTotalMinor);
    }

    [Fact]
    public void Build_ForecastOnlyCountsWhatIsDueAfterTodayAndByTheEndOfTheMonth()
    {
        var today = new DateTime(2026, 10, 20);
        var data = Data(
            subscriptions:
            [
                Samples.Subscription(1_000, new DateTime(2026, 10, 19)),
                Samples.Subscription(2_000, new DateTime(2026, 10, 20)),
                Samples.Subscription(4_000, new DateTime(2026, 10, 21)),
                Samples.Subscription(8_000, new DateTime(2026, 10, 31)),
                Samples.Subscription(16_000, new DateTime(2026, 11, 1))
            ],
            recurringIncomes:
            [
                Samples.RecurringIncome(500, new DateTime(2026, 10, 20)),
                Samples.RecurringIncome(700, new DateTime(2026, 10, 31)),
                Samples.RecurringIncome(900, new DateTime(2026, 11, 1))
            ],
            initialBalance: 100_000);

        var result = FinancialSummaryCalculator.Build(data, today);

        Assert.Equal(100_000 + 700 - (4_000 + 8_000), result.ForecastBalanceMinor);
    }

    [Fact]
    public void Build_IgnoresDeletedSubscriptionsAndRecurringIncomes()
    {
        var deletedSubscription = Samples.Subscription(9_000, new DateTime(2026, 10, 25));
        deletedSubscription.DeletedAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var deletedIncome = Samples.RecurringIncome(9_000, new DateTime(2026, 10, 25));
        deletedIncome.DeletedAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var data = Data(subscriptions: [deletedSubscription], recurringIncomes: [deletedIncome], initialBalance: 1_000);

        var result = FinancialSummaryCalculator.Build(data, new DateTime(2026, 10, 3));

        Assert.Equal(1_000, result.ForecastBalanceMinor);
    }
}
