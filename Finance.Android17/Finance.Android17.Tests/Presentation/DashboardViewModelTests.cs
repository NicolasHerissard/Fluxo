using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Presentation;

public sealed class DashboardViewModelTests : IDisposable
{
    private readonly AppFixture _app = new();

    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task Load_ShowsMonthTotals_Comparison_AndTopCategory()
    {
        await _app.SeedScenarioAsync();
        var vm = _app.CreateDashboard();

        await vm.LoadAsync();

        Assert.Equal("Bonjour", vm.Greeting);
        Assert.Equal("Mardi 20 Octobre", vm.CurrentDate);
        Assert.Equal("Ce mois", vm.PeriodTitle);
        Assert.Equal("202,50 €", vm.Total.Fr());
        Assert.Equal("3 dépenses", vm.ExpenseCount);
        Assert.Equal("Alimentation", vm.TopCategory);
        Assert.Equal("↑ 153 % par rapport à la période précédente", vm.Comparison);
        Assert.Equal("#FFDAD6", vm.ComparisonColor);
        Assert.True(vm.HasData);
        Assert.False(vm.IsEmpty);
        Assert.False(vm.IsSkeletonVisible);
        Assert.True(vm.IsDashboardVisible);
    }

    [Fact]
    public async Task Load_ShowsBalanceNetForecastAndSavingsRate()
    {
        await _app.SeedScenarioAsync();
        var vm = _app.CreateDashboard();

        await vm.LoadAsync();

        Assert.Equal("1 717,50 €", vm.CurrentBalance.Fr());
        Assert.Equal("2 000,00 €", vm.MonthlyIncome.Fr());
        Assert.Equal("1 797,50 €", vm.MonthlyNet.Fr());
        Assert.Equal("1 751,51 €", vm.ForecastBalance.Fr());
        Assert.Equal("Après échéances connues", vm.ForecastStatus);
        Assert.Equal("90%", vm.SavingsRate.Compact());
    }

    [Fact]
    public async Task Load_FlagsAProbableOverdraft_WhenForecastIsNegative()
    {
        var food = await _app.CategoryAsync("Alimentation");
        await _app.Finance.AddExpenseAsync(10_000, "Courses", new DateTime(2026, 10, 3), food.Id);
        var vm = _app.CreateDashboard();

        await vm.LoadAsync();

        Assert.Equal("Découvert probable en fin de mois", vm.ForecastStatus);
    }

    [Fact]
    public async Task Load_BuildsCategoryBars_WithBudgetProgress()
    {
        await _app.SeedScenarioAsync();
        var vm = _app.CreateDashboard();

        await vm.LoadAsync();

        Assert.Equal(["Alimentation", "Transport"], vm.CategoryBars.Select(x => x.Label));
        var food = vm.CategoryBars[0];
        Assert.Equal("162,50 €", food.Amount.Fr());
        Assert.Equal("80%", food.Percent.Compact());
        Assert.Equal(16_250d / 20_250d, food.Ratio, 6);
        Assert.True(food.HasBudget);
        Assert.Equal("Budget : 162,50 € / 200,00 €", food.BudgetText.Fr());
        Assert.Equal(0.8125, food.BudgetProgress, 6);
        Assert.Equal("#FFD8A8", food.BudgetColor);
        var transport = vm.CategoryBars[1];
        Assert.False(transport.HasBudget);
        Assert.Equal(string.Empty, transport.BudgetText);
        Assert.Equal(transport.Color, transport.BudgetColor);
    }

    [Fact]
    public async Task Load_ListsTheThreeMostRecentExpenses()
    {
        await _app.SeedScenarioAsync();
        var vm = _app.CreateDashboard();

        await vm.LoadAsync();

        Assert.Equal(["Train", "Courses", "Pain"], vm.RecentExpenses.Select(x => x.Description));
        var train = vm.RecentExpenses[0];
        Assert.Equal("Transport", train.Category);
        Assert.Equal("T", train.CategoryInitial);
        Assert.Equal("40,00 €", train.Amount.Fr());
        Assert.Equal("15 oct. 2026", train.Date);
    }

    [Fact]
    public async Task Load_ShowsTheNextSubscription()
    {
        await _app.SeedScenarioAsync();
        var vm = _app.CreateDashboard();

        await vm.LoadAsync();

        Assert.Equal("Netflix · 25 oct.", vm.NextSubscription);
    }

    [Fact]
    public async Task Load_ShowsMonthlyBudgetStatus_AndItsThresholdColors()
    {
        await _app.SeedScenarioAsync();
        _app.Settings.MonthlyBudgetMinor = 25_000;
        var vm = _app.CreateDashboard();

        await vm.LoadAsync();

        Assert.True(vm.IsBudgetVisible);
        Assert.Equal("47,50 € disponibles sur le budget", vm.BudgetStatus.Fr());
        Assert.Equal(0.81, vm.BudgetProgress, 6);
        Assert.Equal("#FFD8A8", vm.BudgetColor);

        _app.Settings.MonthlyBudgetMinor = 20_000;
        await vm.LoadAsync();

        Assert.Equal("Budget dépassé de 2,50 €", vm.BudgetStatus.Fr());
        Assert.Equal(1, vm.BudgetProgress);
        Assert.Equal("#FFB4AB", vm.BudgetColor);

        _app.Settings.MonthlyBudgetMinor = 100_000;
        await vm.LoadAsync();

        Assert.Equal("#B7F5C5", vm.BudgetColor);
    }

    [Fact]
    public async Task Load_HidesTheMonthlyBudget_OutsideTheMonthPeriod()
    {
        await _app.SeedScenarioAsync();
        _app.Settings.MonthlyBudgetMinor = 25_000;
        var vm = _app.CreateDashboard();

        await vm.SelectPeriodCommand.ExecuteAsync("Année");

        Assert.False(vm.IsBudgetVisible);
    }

    [Fact]
    public async Task SelectPeriodWeek_ComparesWithThePreviousWeek()
    {
        await _app.SeedScenarioAsync();
        var vm = _app.CreateDashboard();

        await vm.SelectPeriodCommand.ExecuteAsync("Semaine");

        Assert.Equal("Cette semaine", vm.PeriodTitle);
        Assert.True(vm.IsEmpty);
        Assert.Equal("0 dépense", vm.ExpenseCount);
        Assert.Equal("↓ 100 % par rapport à la période précédente", vm.Comparison);
        Assert.Equal("#B7F5C5", vm.ComparisonColor);
        Assert.Equal("#6750A4", vm.WeekBackground);
        Assert.Equal("Transparent", vm.MonthBackground);
    }

    [Fact]
    public async Task SelectPeriodYear_HasNoPreviousYearToCompareWith()
    {
        await _app.SeedScenarioAsync();
        var vm = _app.CreateDashboard();

        await vm.SelectPeriodCommand.ExecuteAsync("Année");

        Assert.Equal("Cette année", vm.PeriodTitle);
        Assert.Equal("282,50 €", vm.Total.Fr());
        Assert.Equal("4 dépenses", vm.ExpenseCount);
        Assert.Equal("Aucune donnée sur la période précédente", vm.Comparison);
    }

    [Fact]
    public async Task Load_OnAnEmptyDatabase_ShowsEmptyState()
    {
        var vm = _app.CreateDashboard();

        await vm.LoadAsync();

        Assert.True(vm.IsEmpty);
        Assert.False(vm.HasData);
        Assert.Equal("—", vm.TopCategory);
        Assert.Equal("Aucun abonnement", vm.NextSubscription);
        Assert.Equal("0,00 €", vm.CurrentBalance.Fr());
        Assert.Empty(vm.CategoryBars);
        Assert.Empty(vm.RecentExpenses);
    }

    [Fact]
    public async Task TogglePrivacy_MasksEveryAmount()
    {
        await _app.SeedScenarioAsync();
        var vm = _app.CreateDashboard();
        await vm.LoadAsync();

        await vm.TogglePrivacyCommand.ExecuteAsync(null);

        Assert.True(_app.Settings.HideAmounts);
        Assert.Equal("••••••", vm.Total);
        Assert.Equal("••••", vm.CurrentBalance);
        Assert.Equal("••••", vm.CategoryBars[0].Amount);
        Assert.Equal("••••", vm.RecentExpenses[0].Amount);
        Assert.Equal("icon_visibility_off.png", vm.PrivacyIcon);
    }

    [Fact]
    public async Task Greeting_BecomesEvening_AfterSixPm()
    {
        using var evening = new AppFixture(new DateTime(2026, 10, 20, 19, 0, 0));
        var vm = evening.CreateDashboard();

        await vm.LoadAsync();

        Assert.Equal("Bonsoir", vm.Greeting);
    }

    [Fact]
    public async Task Money_UsesTheConfiguredCurrencySymbol()
    {
        await _app.SeedScenarioAsync();
        _app.Settings.CurrencyCode = "USD";
        var vm = _app.CreateDashboard();

        await vm.LoadAsync();

        Assert.Equal("202,50 $", vm.Total.Fr());
    }
}
