using Finance.Android17.Application;
using Finance.Android17.Domain;
using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Dashboard;

public class DashboardCalculatorTests
{
    private static readonly DateTime Today = new(2026, 10, 20);

    private static FinanceData Data(
        IEnumerable<Expense>? expenses = null,
        IEnumerable<Subscription>? subscriptions = null,
        IEnumerable<Income>? incomes = null,
        long initialBalance = 0) =>
        new([.. expenses ?? []], [.. incomes ?? []], [.. subscriptions ?? []], [], initialBalance);

    [Fact]
    public void Build_UsesOnlyTheCurrentMonth_AndFindsTheTopCategory()
    {
        var food = Samples.Category("Alimentation");
        var transport = Samples.Category("Transport");
        var data = Data(
        [
            Samples.Expense(1_250, new DateTime(2026, 9, 2), food),
            Samples.Expense(800, new DateTime(2026, 9, 3), food),
            Samples.Expense(4_000, new DateTime(2026, 8, 3), transport)
        ]);

        var report = DashboardCalculator.Build(data, DashboardPeriod.Month, new DateTime(2026, 9, 25), 0);

        Assert.Equal(2_050, report.TotalMinor);
        Assert.Equal(2, report.ExpenseCount);
        Assert.Equal("Alimentation", report.TopCategoryName);
        Assert.Equal(TrendDirection.Down, report.Comparison.Direction);
        Assert.Equal(48.75, report.Comparison.Percent, 2);
    }

    [Fact]
    public void Build_GroupsByCategory_BiggestFirst_WithTheirShareOfTheTotal()
    {
        var food = Samples.Category("Alimentation", "#00FF00", budgetMinor: 5_000);
        var transport = Samples.Category("Transport", "#0000FF");
        var data = Data(
        [
            Samples.Expense(1_000, new DateTime(2026, 10, 2), transport),
            Samples.Expense(2_000, new DateTime(2026, 10, 3), food),
            Samples.Expense(1_000, new DateTime(2026, 10, 4), food)
        ]);

        var report = DashboardCalculator.Build(data, DashboardPeriod.Month, Today, 0);

        Assert.Collection(report.Categories,
            first =>
            {
                Assert.Equal("Alimentation", first.Name);
                Assert.Equal("#00FF00", first.Color);
                Assert.Equal(3_000, first.TotalMinor);
                Assert.Equal(0.75, first.Share, 3);
                Assert.Equal(new BudgetStatus(5_000, 3_000), first.Budget);
            },
            second =>
            {
                Assert.Equal("Transport", second.Name);
                Assert.Equal(0.25, second.Share, 3);
                Assert.Null(second.Budget);
            });
    }

    [Fact]
    public void Build_KeepsTwoCategoriesWithTheSameNameApart()
    {
        var first = Samples.Category("Sorties");
        var second = Samples.Category("Sorties");
        var data = Data(
        [
            Samples.Expense(1_000, new DateTime(2026, 10, 2), first),
            Samples.Expense(3_000, new DateTime(2026, 10, 3), second)
        ]);

        var report = DashboardCalculator.Build(data, DashboardPeriod.Month, Today, 0);

        Assert.Equal([3_000, 1_000], report.Categories.Select(x => x.TotalMinor));
    }

    [Fact]
    public void Build_LabelsExpensesWithoutCategory()
    {
        var data = Data([Samples.Expense(1_000, new DateTime(2026, 10, 2))]);

        var report = DashboardCalculator.Build(data, DashboardPeriod.Month, Today, 0);

        var spend = Assert.Single(report.Categories);
        Assert.Equal(Uncategorized.Name, spend.Name);
        Assert.Equal(Uncategorized.Color, spend.Color);
    }

    [Fact]
    public void Build_WithoutExpenses_HasNoCategoryAndNoDivisionByZero()
    {
        var report = DashboardCalculator.Build(Data(), DashboardPeriod.Month, Today, 0);

        Assert.Equal(0, report.TotalMinor);
        Assert.Equal(0, report.ExpenseCount);
        Assert.Empty(report.Categories);
        Assert.Null(report.TopCategoryName);
        Assert.Equal(TrendDirection.NoReference, report.Comparison.Direction);
    }

    [Fact]
    public void Build_ForTheWeek_OnlyCountsSinceMonday()
    {
        var data = Data(
        [
            Samples.Expense(100, new DateTime(2026, 10, 18)),
            Samples.Expense(200, new DateTime(2026, 10, 19)),
            Samples.Expense(400, new DateTime(2026, 10, 20)),
            Samples.Expense(1_000, new DateTime(2026, 10, 14))
        ]);

        var report = DashboardCalculator.Build(data, DashboardPeriod.Week, Today, 0);

        Assert.Equal(600, report.TotalMinor);
        Assert.Equal(TrendDirection.Down, report.Comparison.Direction);
        Assert.Equal(45.45, report.Comparison.Percent, 2);
    }

    [Fact]
    public void Build_ForTheYear_CountsSinceJanuaryFirst()
    {
        var data = Data(
        [
            Samples.Expense(100, new DateTime(2025, 12, 31)),
            Samples.Expense(200, new DateTime(2026, 1, 1)),
            Samples.Expense(400, new DateTime(2026, 10, 20))
        ]);

        var report = DashboardCalculator.Build(data, DashboardPeriod.Year, Today, 0);

        Assert.Equal(600, report.TotalMinor);
        Assert.Equal(TrendDirection.Up, report.Comparison.Direction);
    }

    [Fact]
    public void Build_TracksTheMonthlyBudget_OnlyOverTheMonth()
    {
        var data = Data([Samples.Expense(8_500, new DateTime(2026, 10, 5))]);

        var month = DashboardCalculator.Build(data, DashboardPeriod.Month, Today, 10_000);
        var week = DashboardCalculator.Build(data, DashboardPeriod.Week, Today, 10_000);
        var none = DashboardCalculator.Build(data, DashboardPeriod.Month, Today, 0);

        Assert.Equal(new BudgetStatus(10_000, 8_500), month.MonthlyBudget);
        Assert.Equal(BudgetLevel.Warning, month.MonthlyBudget!.Level);
        Assert.Null(week.MonthlyBudget);
        Assert.Null(none.MonthlyBudget);
    }

    [Fact]
    public void Build_PicksTheNextSubscriptionDueTodayOrLater()
    {
        var data = Data(subscriptions:
        [
            Samples.Subscription(1_000, new DateTime(2026, 10, 19), name: "Passé"),
            Samples.Subscription(2_000, new DateTime(2026, 11, 3), name: "Plus tard"),
            Samples.Subscription(3_000, new DateTime(2026, 10, 20), name: "Aujourd’hui")
        ]);

        var report = DashboardCalculator.Build(data, DashboardPeriod.Month, Today, 0);

        Assert.Equal("Aujourd’hui", report.NextSubscription?.Name);
    }

    [Fact]
    public void Build_WithoutUpcomingSubscription_HasNone()
    {
        var data = Data(subscriptions: [Samples.Subscription(1_000, new DateTime(2026, 10, 19))]);

        Assert.Null(DashboardCalculator.Build(data, DashboardPeriod.Month, Today, 0).NextSubscription);
    }

    [Fact]
    public void Build_ListsTheThreeLatestExpenses_WhateverThePeriod()
    {
        var created = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
        var data = Data(
        [
            Samples.Expense(1, new DateTime(2026, 1, 5), description: "Ancienne"),
            Samples.Expense(2, new DateTime(2026, 10, 10), description: "A", createdAtUtc: created),
            Samples.Expense(3, new DateTime(2026, 10, 10), description: "B", createdAtUtc: created.AddHours(1)),
            Samples.Expense(4, new DateTime(2026, 10, 12), description: "C"),
            Samples.Expense(5, new DateTime(2026, 10, 1), description: "D")
        ]);

        var report = DashboardCalculator.Build(data, DashboardPeriod.Week, Today, 0);

        Assert.Equal(["C", "B", "A"], report.RecentExpenses.Select(x => x.Description));
    }

    [Fact]
    public void Build_EmbedsTheFinancialSummary()
    {
        var data = Data(
            expenses: [Samples.Expense(2_000, new DateTime(2026, 10, 5))],
            incomes: [Samples.Income(10_000, new DateTime(2026, 10, 1))],
            initialBalance: 5_000);

        var report = DashboardCalculator.Build(data, DashboardPeriod.Week, Today, 0);

        Assert.Equal(13_000, report.Financial.CurrentBalanceMinor);
        Assert.Equal(8_000, report.Financial.NetTotalMinor);
    }
}
