using Finance.Android17.Application;
using Finance.Android17.Domain;
using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Dashboard;

public class CashflowCalculatorTests
{
    private static readonly DateTime Today = new(2026, 10, 20);

    private static FinanceData Data(IEnumerable<Expense>? expenses = null, IEnumerable<Income>? incomes = null) =>
        new([.. expenses ?? []], [.. incomes ?? []], [], [], 0);

    [Fact]
    public void Build_ReturnsTheLastSixMonths_OldestFirst_EndingWithTheCurrentOne()
    {
        var flows = CashflowCalculator.Build(Data(), Today);

        Assert.Equal(6, flows.Count);
        Assert.Equal(new DateTime(2026, 5, 1), flows[0].Month);
        Assert.Equal(new DateTime(2026, 10, 1), flows[^1].Month);
    }

    [Fact]
    public void Build_SumsExpensesAndIncomes_PerCalendarMonth()
    {
        var data = Data(
            [
                Samples.Expense(1_000, new DateTime(2026, 10, 1)),
                Samples.Expense(2_000, new DateTime(2026, 10, 31)),
                Samples.Expense(500, new DateTime(2026, 9, 30)),
                Samples.Expense(9_999, new DateTime(2026, 4, 30)),
                Samples.Expense(9_999, new DateTime(2026, 11, 1))
            ],
            [
                Samples.Income(100_000, new DateTime(2026, 10, 5)),
                Samples.Income(7_000, new DateTime(2026, 7, 14))
            ]);

        var flows = CashflowCalculator.Build(data, Today);

        Assert.Equal((3_000, 100_000), (flows[5].ExpenseMinor, flows[5].IncomeMinor));
        Assert.Equal((500, 0), (flows[4].ExpenseMinor, flows[4].IncomeMinor));
        Assert.Equal((0, 7_000), (flows[2].ExpenseMinor, flows[2].IncomeMinor));
        Assert.Equal(0, flows[0].ExpenseMinor);
    }

    [Fact]
    public void Build_CrossesTheYearBoundary()
    {
        var flows = CashflowCalculator.Build(Data(), new DateTime(2026, 2, 10), months: 4);

        Assert.Equal([new DateTime(2025, 11, 1), new DateTime(2025, 12, 1), new DateTime(2026, 1, 1), new DateTime(2026, 2, 1)], flows.Select(x => x.Month));
    }
}
