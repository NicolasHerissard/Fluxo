using Finance.Android17.Presentation;
using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Presentation;

public sealed class IncomeViewModelTests : IDisposable
{
    private readonly AppFixture _app = new();

    public void Dispose() => _app.Dispose();

    private async Task<IncomeViewModel> LoadedAsync()
    {
        await _app.SeedScenarioAsync();
        var vm = _app.CreateIncome();
        await vm.LoadAsync();
        return vm;
    }

    [Fact]
    public async Task Load_ShowsTheFinancialSummary()
    {
        var vm = await LoadedAsync();

        Assert.Equal("1 717,50 €", vm.CurrentBalance.Fr());
        Assert.Equal("2 000,00 €", vm.MonthIncome.Fr());
        Assert.Equal("1 797,50 €", vm.MonthNet.Fr());
        Assert.Equal("1 751,51 €", vm.ForecastBalance.Fr());
    }

    [Fact]
    public async Task Load_ListsIncomesRecurringIncomesAndGoals()
    {
        await _app.SavingsGoals.AddAsync("Vacances", 100_000, 25_000, new DateTime(2027, 6, 1));
        await _app.RecurringIncomes.AddAsync("Prime", 90_000, 12, new DateTime(2026, 12, 15));
        await _app.RecurringIncomes.AddAsync("Dividendes", 30_000, 3, new DateTime(2026, 12, 20));
        var vm = await LoadedAsync();

        var income = Assert.Single(vm.Incomes);
        Assert.Equal("Salaire", income.Description);
        Assert.Equal("2 000,00 €", income.Amount.Fr());
        Assert.Equal("01 oct. 2026", income.Date);

        Assert.Equal(["Dividendes", "Loyer perçu", "Prime"], vm.RecurringIncomes.Select(x => x.Name).Order());
        Assert.Equal("Annuel", vm.RecurringIncomes.Single(x => x.Name == "Prime").Frequency);
        Assert.Equal("Tous les 3 mois", vm.RecurringIncomes.Single(x => x.Name == "Dividendes").Frequency);
        Assert.Equal("Mensuel", vm.RecurringIncomes.Single(x => x.Name == "Loyer perçu").Frequency);

        var goal = Assert.Single(vm.SavingsGoals);
        Assert.Equal("Vacances", goal.Name);
        Assert.Equal("250,00 €", goal.Saved.Fr());
        Assert.Equal("1 000,00 €", goal.Target.Fr());
        Assert.Equal("25%", goal.ProgressText.Compact());
        Assert.Equal(0.25, goal.Progress, 6);
    }

    [Fact]
    public async Task Amounts_AreHidden_WhenPrivacyIsOn()
    {
        _app.Settings.HideAmounts = true;
        await _app.SavingsGoals.AddAsync("Vacances", 100_000, 25_000, new DateTime(2027, 6, 1));
        var vm = await LoadedAsync();

        Assert.Equal("••••", vm.CurrentBalance);
        Assert.NotEmpty(vm.Incomes);
        Assert.All(vm.Incomes, x => Assert.Equal("••••", x.Amount));
        Assert.NotEmpty(vm.RecurringIncomes);
        Assert.All(vm.RecurringIncomes, x => Assert.Equal("••••", x.Amount));
        var goal = Assert.Single(vm.SavingsGoals);
        Assert.Equal(("••••", "••••"), (goal.Saved, goal.Target));
    }

    [Fact]
    public async Task SaveIncome_AddsThenEditsThenDeletes()
    {
        var vm = _app.CreateIncome();

        vm.IncomeDescription = "Prime";
        vm.IncomeAmount = "150,25";
        vm.IncomeDate = new DateTime(2026, 10, 18);
        await vm.SaveIncomeCommand.ExecuteAsync(null);

        var row = Assert.Single(vm.Incomes);
        Assert.Equal("Revenu enregistré.", vm.StatusMessage);
        Assert.Equal("150,25 €", row.Amount.Fr());
        Assert.Equal(string.Empty, vm.IncomeDescription);

        vm.EditIncomeCommand.Execute(row);
        Assert.Equal("Modifier le revenu", vm.IncomeFormTitle);
        Assert.Equal("150,25", vm.IncomeAmount);
        vm.IncomeAmount = "200";
        await vm.SaveIncomeCommand.ExecuteAsync(null);

        row = Assert.Single(vm.Incomes);
        Assert.Equal("200,00 €", row.Amount.Fr());
        Assert.Equal("Ajouter un revenu", vm.IncomeFormTitle);

        await vm.DeleteIncomeCommand.ExecuteAsync(row);
        Assert.Empty(vm.Incomes);
    }

    [Theory]
    [InlineData("", "10", "Indiquez l’origine du revenu.")]
    [InlineData("Prime", "0", "Indiquez un montant valide.")]
    [InlineData("Prime", "x", "Indiquez un montant valide.")]
    public async Task SaveIncome_ValidatesInput(string description, string amount, string message)
    {
        var vm = _app.CreateIncome();

        vm.IncomeDescription = description;
        vm.IncomeAmount = amount;
        await vm.SaveIncomeCommand.ExecuteAsync(null);

        Assert.Equal(message, vm.StatusMessage);
        Assert.Empty(await _app.Incomes.GetAllAsync());
    }

    [Fact]
    public async Task SaveIncome_WhenRepeating_StoresARecurringIncomeWithTheChosenFrequency()
    {
        var vm = _app.CreateIncome();

        vm.ShowIncomeFormCommand.Execute(null);
        vm.IncomeDescription = "Pension";
        vm.IncomeAmount = "300";
        vm.IncomeRepeats = true;
        vm.SelectedFrequency = "Trimestriel";
        vm.IncomeDate = new DateTime(2026, 11, 5);
        await vm.SaveIncomeCommand.ExecuteAsync(null);

        var stored = Assert.Single(await _app.RecurringIncomes.GetAllAsync());
        Assert.Equal("Pension", stored.Name);
        Assert.Equal(3, stored.IntervalMonths);
        Assert.Equal(new DateTime(2026, 11, 5), stored.NextOccurrenceDate);
        Assert.Empty(await _app.Incomes.GetAllAsync());
        Assert.Equal("Revenu récurrent enregistré.", vm.StatusMessage);
        Assert.False(vm.IncomeRepeats);
        Assert.False(vm.IsIncomeFormVisible);
        Assert.Equal("Mensuel", vm.SelectedFrequency);
    }

    [Fact]
    public async Task EditRecurringIncome_ReusesTheIncomeForm_AndKeepsItRecurring()
    {
        await _app.RecurringIncomes.AddAsync("Prime", 90_000, 12, new DateTime(2026, 12, 15));
        var vm = _app.CreateIncome();
        await vm.LoadAsync();

        vm.EditRecurringIncomeCommand.Execute(vm.RecurringIncomes.Single());

        Assert.True(vm.IsIncomeFormVisible);
        Assert.True(vm.IncomeRepeats);
        Assert.False(vm.CanChooseRepeat);
        Assert.Equal("Modifier le revenu récurrent", vm.IncomeFormTitle);
        Assert.Equal("Annuel", vm.SelectedFrequency);
        Assert.Equal(new DateTime(2026, 12, 15), vm.IncomeDate);

        vm.IncomeAmount = "1000";
        await vm.SaveIncomeCommand.ExecuteAsync(null);

        var stored = Assert.Single(await _app.RecurringIncomes.GetAllAsync());
        Assert.Equal(100_000, stored.AmountMinor);
        Assert.Empty(await _app.Incomes.GetAllAsync());
    }

    [Fact]
    public async Task IncomeForm_IsClosedUntilAsked_AndClosesOnCancel()
    {
        var vm = _app.CreateIncome();
        Assert.False(vm.IsIncomeFormVisible);
        Assert.True(vm.IsIncomeFormClosed);
        Assert.False(vm.IsGoalFormVisible);

        vm.ShowIncomeFormCommand.Execute(null);
        vm.ShowGoalFormCommand.Execute(null);
        Assert.True(vm.IsIncomeFormVisible);
        Assert.False(vm.IsIncomeFormClosed);
        Assert.True(vm.IsGoalFormVisible);
        Assert.True(vm.CanChooseRepeat);

        vm.CancelIncomeEditCommand.Execute(null);
        vm.CancelGoalEditCommand.Execute(null);
        Assert.False(vm.IsIncomeFormVisible);
        Assert.False(vm.IsGoalFormVisible);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Load_ShowsTheRecurringIncomeStillExpectedThisMonth()
    {
        await _app.RecurringIncomes.AddAsync("Loyer", 50_000, 1, new DateTime(2026, 10, 28));
        await _app.RecurringIncomes.AddAsync("Prime", 90_000, 12, new DateTime(2026, 12, 15));
        var vm = _app.CreateIncome();
        await vm.LoadAsync();

        Assert.Equal("500,00 €", vm.ExpectedIncome.Fr());
    }

    [Fact]
    public async Task SaveGoal_StoresOptionalDate_AndValidatesSavedAmount()
    {
        var vm = _app.CreateIncome();

        vm.GoalName = "Voiture";
        vm.GoalTargetAmount = "8000";
        vm.GoalSavedAmount = "-1";
        await vm.SaveGoalCommand.ExecuteAsync(null);
        Assert.Equal("Indiquez un montant épargné valide.", vm.StatusMessage);

        vm.GoalSavedAmount = "500";
        vm.GoalHasTargetDate = true;
        vm.GoalTargetDate = new DateTime(2027, 3, 1);
        await vm.SaveGoalCommand.ExecuteAsync(null);

        var goal = Assert.Single(await _app.SavingsGoals.GetAllAsync());
        Assert.Equal(800_000, goal.TargetAmountMinor);
        Assert.Equal(50_000, goal.SavedAmountMinor);
        Assert.Equal(new DateTime(2027, 3, 1), goal.TargetDate);
        Assert.Equal("Objectif enregistré.", vm.StatusMessage);
        Assert.False(vm.GoalHasTargetDate);
        Assert.Equal(new DateTime(2027, 10, 20), vm.GoalTargetDate);

        vm.EditGoalCommand.Execute(vm.SavingsGoals.Single());
        Assert.Equal("Modifier l’objectif", vm.GoalFormTitle);
        Assert.True(vm.GoalHasTargetDate);
        Assert.Equal("500", vm.GoalSavedAmount);
    }
}
