using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Android17.Application;
using Finance.Android17.Domain;

namespace Finance.Android17.Presentation;

public partial class IncomeViewModel(
    IIncomeRepository incomeRepository,
    IRecurringIncomeRepository recurringIncomeRepository,
    ISavingsGoalRepository goalRepository,
    IFinancialProfileRepository profileRepository,
    FinanceDataLoader dataLoader,
    ISettingsService settings,
    TimeProvider clock) : ObservableObject
{
    private IReadOnlyList<Income> _incomes = [];
    private IReadOnlyList<RecurringIncome> _recurringIncomes = [];
    private IReadOnlyList<SavingsGoal> _goals = [];
    private Guid? _editingIncomeId;
    private Guid? _editingRecurringIncomeId;
    private Guid? _editingGoalId;

    public ObservableCollection<IncomeRow> Incomes { get; } = [];
    public ObservableCollection<RecurringIncomeRow> RecurringIncomes { get; } = [];
    public ObservableCollection<SavingsGoalRow> SavingsGoals { get; } = [];
    public ObservableCollection<OperationRow> RecentOperations { get; } = [];
    public IReadOnlyList<string> Frequencies { get; } = BillingFrequency.Options;

    [ObservableProperty] private string initialBalance = string.Empty;
    [ObservableProperty] private string currentBalance = "0,00 €";
    [ObservableProperty] private string monthIncome = "0,00 €";
    [ObservableProperty] private string monthNet = "0,00 €";
    [ObservableProperty] private string forecastBalance = "0,00 €";
    [ObservableProperty] private string incomeDescription = string.Empty;
    [ObservableProperty] private string incomeAmount = string.Empty;
    [ObservableProperty] private DateTime incomeDate = clock.GetLocalNow().Date;
    [ObservableProperty] private string incomeFormTitle = "Ajouter un revenu";
    [ObservableProperty] private string recurringName = string.Empty;
    [ObservableProperty] private string recurringAmount = string.Empty;
    [ObservableProperty] private string selectedFrequency = BillingFrequency.Monthly;
    [ObservableProperty] private DateTime nextOccurrenceDate = clock.GetLocalNow().Date.AddMonths(1);
    [ObservableProperty] private string recurringFormTitle = "Ajouter un revenu récurrent";
    [ObservableProperty] private string goalName = string.Empty;
    [ObservableProperty] private string goalTargetAmount = string.Empty;
    [ObservableProperty] private string goalSavedAmount = string.Empty;
    [ObservableProperty] private DateTime goalTargetDate = clock.GetLocalNow().Date.AddYears(1);
    [ObservableProperty] private bool goalHasTargetDate;
    [ObservableProperty] private string goalFormTitle = "Ajouter un objectif";
    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private bool isBusy;

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var data = await dataLoader.LoadAsync();
            _incomes = data.Incomes;
            _recurringIncomes = data.RecurringIncomes;
            _goals = await goalRepository.GetAllAsync();
            InitialBalance = MoneyFormatter.PlainAmount(data.InitialBalanceMinor);

            Incomes.Clear();
            foreach (var item in _incomes)
                Incomes.Add(new(item.Id, item.Description, MoneyFormatter.Date(item.OccurredOn), Money(item.AmountMinor)));

            RecurringIncomes.Clear();
            foreach (var item in _recurringIncomes)
                RecurringIncomes.Add(new(item.Id, item.Name, Money(item.AmountMinor), BillingFrequency.ToDisplay(item.IntervalMonths), MoneyFormatter.Date(item.NextOccurrenceDate)));

            SavingsGoals.Clear();
            foreach (var item in _goals)
                SavingsGoals.Add(new(item.Id, item.Name, Money(item.SavedAmountMinor), Money(item.TargetAmountMinor), MoneyFormatter.Percent(item.Progress), item.Progress));

            RecentOperations.Clear();
            foreach (var operation in OperationsFeed.Recent(data.Expenses, data.Incomes))
            {
                var isIncome = operation.Kind == OperationKind.Income;
                var type = isIncome ? "Revenu" : "Dépense";
                RecentOperations.Add(new(type, string.IsNullOrWhiteSpace(operation.Description) ? type : operation.Description,
                    MoneyFormatter.Date(operation.OccurredOn), HiddenMoney(operation.SignedAmountMinor), isIncome ? Palette.IncomeAmount : Palette.ExpenseAmount));
            }

            var summary = FinancialSummaryCalculator.Build(data, clock.GetLocalNow().Date);
            CurrentBalance = HiddenMoney(summary.CurrentBalanceMinor);
            MonthIncome = HiddenMoney(summary.IncomeTotalMinor);
            MonthNet = HiddenMoney(summary.NetTotalMinor);
            ForecastBalance = HiddenMoney(summary.ForecastBalanceMinor);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveInitialBalanceAsync()
    {
        if (!AmountParser.TryParse(InitialBalance, out var amount)) { StatusMessage = "Indiquez un solde initial valide."; return; }
        await profileRepository.SetInitialBalanceAsync(amount);
        StatusMessage = "Solde initial enregistré.";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task SaveIncomeAsync()
    {
        if (string.IsNullOrWhiteSpace(IncomeDescription)) { StatusMessage = "Indiquez l’origine du revenu."; return; }
        if (!AmountParser.TryParse(IncomeAmount, out var amount) || amount <= 0) { StatusMessage = "Indiquez un montant valide."; return; }
        if (_editingIncomeId is not Guid id) await incomeRepository.AddAsync(amount, IncomeDescription, IncomeDate, settings.CurrencyCode);
        else await incomeRepository.UpdateAsync(id, amount, IncomeDescription, IncomeDate, settings.CurrencyCode);
        CancelIncomeEdit();
        StatusMessage = "Revenu enregistré.";
        await LoadAsync();
    }

    [RelayCommand]
    private void EditIncome(IncomeRow row)
    {
        var income = _incomes.FirstOrDefault(x => x.Id == row.Id);
        if (income is null) return;
        _editingIncomeId = income.Id;
        IncomeDescription = income.Description;
        IncomeAmount = MoneyFormatter.PlainAmount(income.AmountMinor);
        IncomeDate = income.OccurredOn;
        IncomeFormTitle = "Modifier le revenu";
    }

    [RelayCommand]
    private async Task DeleteIncomeAsync(IncomeRow row)
    {
        await incomeRepository.DeleteAsync(row.Id);
        if (_editingIncomeId == row.Id) CancelIncomeEdit();
        await LoadAsync();
    }

    [RelayCommand]
    private void CancelIncomeEdit()
    {
        _editingIncomeId = null;
        IncomeDescription = IncomeAmount = string.Empty;
        IncomeDate = clock.GetLocalNow().Date;
        IncomeFormTitle = "Ajouter un revenu";
    }

    [RelayCommand]
    private async Task SaveRecurringIncomeAsync()
    {
        if (string.IsNullOrWhiteSpace(RecurringName)) { StatusMessage = "Indiquez le nom du revenu récurrent."; return; }
        if (!AmountParser.TryParse(RecurringAmount, out var amount) || amount <= 0) { StatusMessage = "Indiquez un montant valide."; return; }
        var interval = BillingFrequency.ToMonths(SelectedFrequency);
        if (_editingRecurringIncomeId is not Guid id) await recurringIncomeRepository.AddAsync(RecurringName, amount, interval, NextOccurrenceDate, settings.CurrencyCode);
        else await recurringIncomeRepository.UpdateAsync(id, RecurringName, amount, interval, NextOccurrenceDate, settings.CurrencyCode);
        CancelRecurringIncomeEdit();
        StatusMessage = "Revenu récurrent enregistré.";
        await LoadAsync();
    }

    [RelayCommand]
    private void EditRecurringIncome(RecurringIncomeRow row)
    {
        var recurring = _recurringIncomes.FirstOrDefault(x => x.Id == row.Id);
        if (recurring is null) return;
        _editingRecurringIncomeId = recurring.Id;
        RecurringName = recurring.Name;
        RecurringAmount = MoneyFormatter.PlainAmount(recurring.AmountMinor);
        SelectedFrequency = BillingFrequency.ToOption(recurring.IntervalMonths);
        NextOccurrenceDate = recurring.NextOccurrenceDate;
        RecurringFormTitle = "Modifier le revenu récurrent";
    }

    [RelayCommand]
    private async Task DeleteRecurringIncomeAsync(RecurringIncomeRow row)
    {
        await recurringIncomeRepository.DeleteAsync(row.Id);
        if (_editingRecurringIncomeId == row.Id) CancelRecurringIncomeEdit();
        await LoadAsync();
    }

    [RelayCommand]
    private void CancelRecurringIncomeEdit()
    {
        _editingRecurringIncomeId = null;
        RecurringName = RecurringAmount = string.Empty;
        SelectedFrequency = BillingFrequency.Monthly;
        NextOccurrenceDate = clock.GetLocalNow().Date.AddMonths(1);
        RecurringFormTitle = "Ajouter un revenu récurrent";
    }

    [RelayCommand]
    private async Task SaveGoalAsync()
    {
        if (string.IsNullOrWhiteSpace(GoalName)) { StatusMessage = "Indiquez le nom de l’objectif."; return; }
        if (!AmountParser.TryParse(GoalTargetAmount, out var target) || target <= 0) { StatusMessage = "Indiquez un objectif valide."; return; }
        var saved = 0L;
        if (!string.IsNullOrWhiteSpace(GoalSavedAmount) && (!AmountParser.TryParse(GoalSavedAmount, out saved) || saved < 0)) { StatusMessage = "Indiquez un montant épargné valide."; return; }
        DateTime? targetDate = GoalHasTargetDate ? GoalTargetDate.Date : null;
        if (_editingGoalId is not Guid id) await goalRepository.AddAsync(GoalName, target, saved, targetDate);
        else await goalRepository.UpdateAsync(id, GoalName, target, saved, targetDate);
        CancelGoalEdit();
        StatusMessage = "Objectif enregistré.";
        await LoadAsync();
    }

    [RelayCommand]
    private void EditGoal(SavingsGoalRow row)
    {
        var goal = _goals.FirstOrDefault(x => x.Id == row.Id);
        if (goal is null) return;
        _editingGoalId = goal.Id;
        GoalName = goal.Name;
        GoalTargetAmount = MoneyFormatter.PlainAmount(goal.TargetAmountMinor);
        GoalSavedAmount = MoneyFormatter.PlainAmount(goal.SavedAmountMinor);
        GoalHasTargetDate = goal.TargetDate is not null;
        GoalTargetDate = goal.TargetDate ?? clock.GetLocalNow().Date.AddYears(1);
        GoalFormTitle = "Modifier l’objectif";
    }

    [RelayCommand]
    private async Task DeleteGoalAsync(SavingsGoalRow row)
    {
        await goalRepository.DeleteAsync(row.Id);
        if (_editingGoalId == row.Id) CancelGoalEdit();
        await LoadAsync();
    }

    [RelayCommand]
    private void CancelGoalEdit()
    {
        _editingGoalId = null;
        GoalName = GoalTargetAmount = GoalSavedAmount = string.Empty;
        GoalHasTargetDate = false;
        GoalTargetDate = clock.GetLocalNow().Date.AddYears(1);
        GoalFormTitle = "Ajouter un objectif";
    }

    private string Money(long amount) => MoneyFormatter.Money(amount, settings.CurrencyCode);
    private string HiddenMoney(long amount) => settings.HideAmounts ? MoneyFormatter.Hidden : Money(amount);
}
