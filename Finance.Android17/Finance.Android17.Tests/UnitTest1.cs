namespace Finance.Android17.Tests;

public class DashboardTests
{
    [Fact]
    public void ReceiptParser_RecognizesFrenchTotalDateAndMerchant()
    {
        var result = Finance.Android17.Application.ReceiptParser.Parse("CARREFOUR MARKET\nPain 2,50 €\nTOTAL TTC 12,50 €\n29/09/2026");

        Assert.Equal(1250, result.AmountMinor);
        Assert.Equal(new DateTime(2026, 9, 29), result.OccurredOn);
        Assert.Equal("CARREFOUR MARKET", result.MerchantName);
    }

    [Fact]
    public void BuildDashboard_UsesOnlyCurrentMonth_AndFindsTopCategory()
    {
        var food = new Finance.Android17.Domain.Category { Name = "Alimentation" };
        var transport = new Finance.Android17.Domain.Category { Name = "Transport" };
        var expenses = new[]
        {
            new Finance.Android17.Domain.Expense { AmountMinor = 1250, OccurredOn = new DateTime(2026, 9, 2), Category = food },
            new Finance.Android17.Domain.Expense { AmountMinor = 800, OccurredOn = new DateTime(2026, 9, 3), Category = food },
            new Finance.Android17.Domain.Expense { AmountMinor = 4000, OccurredOn = new DateTime(2026, 8, 3), Category = transport }
        };

        var result = Finance.Android17.Application.FinanceService.BuildDashboard(expenses, new DateTime(2026, 9, 25));

        Assert.Equal(2050, result.MonthTotalMinor);
        Assert.Equal(2, result.ExpenseCount);
        Assert.Equal("Alimentation", result.TopCategory);
    }

    [Fact]
    public void BuildFinancialSummary_ComputesBalanceNetAndForecast()
    {
        var expenses = new[]
        {
            new Finance.Android17.Domain.Expense { AmountMinor = 40_000, OccurredOn = new DateTime(2026, 10, 2) },
            new Finance.Android17.Domain.Expense { AmountMinor = 10_000, OccurredOn = new DateTime(2026, 9, 2) }
        };
        var incomes = new[]
        {
            new Finance.Android17.Domain.Income { AmountMinor = 250_000, OccurredOn = new DateTime(2026, 10, 1) }
        };
        var subscriptions = new[]
        {
            new Finance.Android17.Domain.Subscription { AmountMinor = 12_000, NextBillingDate = new DateTime(2026, 10, 15) }
        };
        var recurringIncomes = new[]
        {
            new Finance.Android17.Domain.RecurringIncome { AmountMinor = 30_000, NextOccurrenceDate = new DateTime(2026, 10, 20) }
        };

        var result = Finance.Android17.Application.FinanceService.BuildFinancialSummary(
            expenses, incomes, 100_000, subscriptions, recurringIncomes, new DateTime(2026, 10, 3));

        Assert.Equal(40_000, result.ExpenseTotalMinor);
        Assert.Equal(250_000, result.IncomeTotalMinor);
        Assert.Equal(210_000, result.NetTotalMinor);
        Assert.Equal(300_000, result.CurrentBalanceMinor);
        Assert.Equal(318_000, result.ForecastBalanceMinor);
        Assert.Equal(0.84, result.SavingsRate, 2);
    }

    [Fact]
    public void BuildFinancialSummary_DoesNotDivideByZeroWithoutIncome()
    {
        var result = Finance.Android17.Application.FinanceService.BuildFinancialSummary(
            [], [], 12_500, [], [], new DateTime(2026, 10, 3));

        Assert.Equal(12_500, result.CurrentBalanceMinor);
        Assert.Equal(0, result.SavingsRate);
    }

    [Fact]
    public void BackupV1_DeserializesWithEmptyV2Data()
    {
        const string json = """
            {
              "FormatVersion": 1,
              "CreatedAtUtc": "2026-10-03T10:00:00Z",
              "Categories": [],
              "Expenses": [],
              "Subscriptions": []
            }
            """;

        var snapshot = System.Text.Json.JsonSerializer.Deserialize<Finance.Android17.Application.FinanceBackupSnapshot>(json);

        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot.FormatVersion);
        Assert.Null(snapshot.Incomes);
        Assert.Equal(0, snapshot.InitialBalanceMinor);
    }
}
