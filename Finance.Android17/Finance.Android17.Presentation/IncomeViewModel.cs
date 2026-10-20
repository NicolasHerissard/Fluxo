using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Android17.Application;
using Finance.Android17.Domain;

namespace Finance.Android17.Presentation;

public sealed record IncomeRow(string Description, string Date, string Amount, Income Source);
public sealed record RecurringIncomeRow(string Name, string Amount, string Frequency, string NextDate, RecurringIncome Source);
public sealed record SavingsGoalRow(string Name, string Saved, string Target, string ProgressText, double Progress, SavingsGoal Source);
public sealed record OperationRow(string Type, string Description, string Date, string Amount, string Color);

public partial class IncomeViewModel(FinanceService finance, ISettingsService settings) : ObservableObject
{
    private Income? _editingIncome;
    private RecurringIncome? _editingRecurringIncome;
    private SavingsGoal? _editingGoal;

    public ObservableCollection<IncomeRow> Incomes { get; } = [];
    public ObservableCollection<RecurringIncomeRow> RecurringIncomes { get; } = [];
    public ObservableCollection<SavingsGoalRow> SavingsGoals { get; } = [];
    public ObservableCollection<OperationRow> RecentOperations { get; } = [];
    public IReadOnlyList<string> Frequencies { get; } = ["Mensuel", "Trimestriel", "Annuel"];

    [ObservableProperty] private string initialBalance = string.Empty;
    [ObservableProperty] private string currentBalance = "0,00 €";
    [ObservableProperty] private string monthIncome = "0,00 €";
    [ObservableProperty] private string monthNet = "0,00 €";
    [ObservableProperty] private string forecastBalance = "0,00 €";
    [ObservableProperty] private string incomeDescription = string.Empty;
    [ObservableProperty] private string incomeAmount = string.Empty;
    [ObservableProperty] private DateTime incomeDate = DateTime.Today;
    [ObservableProperty] private string incomeFormTitle = "Ajouter un revenu";
    [ObservableProperty] private string recurringName = string.Empty;
    [ObservableProperty] private string recurringAmount = string.Empty;
    [ObservableProperty] private string selectedFrequency = "Mensuel";
    [ObservableProperty] private DateTime nextOccurrenceDate = DateTime.Today.AddMonths(1);
    [ObservableProperty] private string recurringFormTitle = "Ajouter un revenu récurrent";
    [ObservableProperty] private string goalName = string.Empty;
    [ObservableProperty] private string goalTargetAmount = string.Empty;
    [ObservableProperty] private string goalSavedAmount = string.Empty;
    [ObservableProperty] private DateTime goalTargetDate = DateTime.Today.AddYears(1);
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
            var incomes = await finance.GetIncomesAsync();
            var recurring = await finance.GetRecurringIncomesAsync();
            var goals = await finance.GetSavingsGoalsAsync();
            var expenses = await finance.GetExpensesAsync();
            var subscriptions = await finance.GetSubscriptionsAsync();
            var balance = await finance.GetInitialBalanceAsync();
            InitialBalance = (balance / 100m).ToString("0.##", CultureInfo.GetCultureInfo("fr-FR"));

            Incomes.Clear();
            foreach (var item in incomes)
                Incomes.Add(new(item.Description, item.OccurredOn.ToString("dd MMM yyyy", CultureInfo.GetCultureInfo("fr-FR")), Money(item.AmountMinor), item));

            RecurringIncomes.Clear();
            foreach (var item in recurring)
                RecurringIncomes.Add(new(item.Name, Money(item.AmountMinor), Frequency(item.IntervalMonths), item.NextOccurrenceDate.ToString("dd MMM yyyy", CultureInfo.GetCultureInfo("fr-FR")), item));

            SavingsGoals.Clear();
            foreach (var item in goals)
                SavingsGoals.Add(new(item.Name, Money(item.SavedAmountMinor), Money(item.TargetAmountMinor), $"{item.Progress:P0}", item.Progress, item));

            RecentOperations.Clear();
            var operations = incomes.Select(x => new { x.OccurredOn, x.CreatedAtUtc, Type = "Revenu", x.Description, AmountMinor = x.AmountMinor, Color = "#2E7D32" })
                .Concat(expenses.Select(x => new { x.OccurredOn, x.CreatedAtUtc, Type = "Dépense", x.Description, AmountMinor = -x.AmountMinor, Color = "#B3261E" }))
                .OrderByDescending(x => x.OccurredOn).ThenByDescending(x => x.CreatedAtUtc).Take(20);
            foreach (var item in operations)
                RecentOperations.Add(new(item.Type, string.IsNullOrWhiteSpace(item.Description) ? item.Type : item.Description,
                    item.OccurredOn.ToString("dd MMM yyyy", CultureInfo.GetCultureInfo("fr-FR")), HiddenMoney(item.AmountMinor), item.Color));

            var summary = FinanceService.BuildFinancialSummary(expenses, incomes, balance, subscriptions, recurring, DateTime.Today);
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
        if (!TrySignedAmount(InitialBalance, out var amount)) { StatusMessage = "Indiquez un solde initial valide."; return; }
        await finance.SetInitialBalanceAsync(amount);
        StatusMessage = "Solde initial enregistré.";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task SaveIncomeAsync()
    {
        if (string.IsNullOrWhiteSpace(IncomeDescription)) { StatusMessage = "Indiquez l’origine du revenu."; return; }
        if (!AddExpenseViewModel.TryAmount(IncomeAmount, out var amount) || amount <= 0) { StatusMessage = "Indiquez un montant valide."; return; }
        if (_editingIncome is null) await finance.AddIncomeAsync(amount, IncomeDescription, IncomeDate, settings.CurrencyCode);
        else await finance.UpdateIncomeAsync(_editingIncome.Id, amount, IncomeDescription, IncomeDate, settings.CurrencyCode);
        CancelIncomeEdit();
        StatusMessage = "Revenu enregistré.";
        await LoadAsync();
    }

    [RelayCommand]
    private void EditIncome(IncomeRow row)
    {
        _editingIncome = row.Source;
        IncomeDescription = row.Source.Description;
        IncomeAmount = AmountText(row.Source.AmountMinor);
        IncomeDate = row.Source.OccurredOn;
        IncomeFormTitle = "Modifier le revenu";
    }

    [RelayCommand]
    private async Task DeleteIncomeAsync(IncomeRow row)
    {
        await finance.DeleteIncomeAsync(row.Source.Id);
        if (_editingIncome?.Id == row.Source.Id) CancelIncomeEdit();
        await LoadAsync();
    }

    [RelayCommand]
    private void CancelIncomeEdit()
    {
        _editingIncome = null;
        IncomeDescription = IncomeAmount = string.Empty;
        IncomeDate = DateTime.Today;
        IncomeFormTitle = "Ajouter un revenu";
    }

    [RelayCommand]
    private async Task SaveRecurringIncomeAsync()
    {
        if (string.IsNullOrWhiteSpace(RecurringName)) { StatusMessage = "Indiquez le nom du revenu récurrent."; return; }
        if (!AddExpenseViewModel.TryAmount(RecurringAmount, out var amount) || amount <= 0) { StatusMessage = "Indiquez un montant valide."; return; }
        var interval = SelectedFrequency switch { "Trimestriel" => 3, "Annuel" => 12, _ => 1 };
        if (_editingRecurringIncome is null) await finance.AddRecurringIncomeAsync(RecurringName, amount, interval, NextOccurrenceDate, settings.CurrencyCode);
        else await finance.UpdateRecurringIncomeAsync(_editingRecurringIncome.Id, RecurringName, amount, interval, NextOccurrenceDate, settings.CurrencyCode);
        CancelRecurringIncomeEdit();
        StatusMessage = "Revenu récurrent enregistré.";
        await LoadAsync();
    }

    [RelayCommand]
    private void EditRecurringIncome(RecurringIncomeRow row)
    {
        _editingRecurringIncome = row.Source;
        RecurringName = row.Source.Name;
        RecurringAmount = AmountText(row.Source.AmountMinor);
        SelectedFrequency = row.Source.IntervalMonths switch { 3 => "Trimestriel", 12 => "Annuel", _ => "Mensuel" };
        NextOccurrenceDate = row.Source.NextOccurrenceDate;
        RecurringFormTitle = "Modifier le revenu récurrent";
    }

    [RelayCommand]
    private async Task DeleteRecurringIncomeAsync(RecurringIncomeRow row)
    {
        await finance.DeleteRecurringIncomeAsync(row.Source.Id);
        if (_editingRecurringIncome?.Id == row.Source.Id) CancelRecurringIncomeEdit();
        await LoadAsync();
    }

    [RelayCommand]
    private void CancelRecurringIncomeEdit()
    {
        _editingRecurringIncome = null;
        RecurringName = RecurringAmount = string.Empty;
        SelectedFrequency = "Mensuel";
        NextOccurrenceDate = DateTime.Today.AddMonths(1);
        RecurringFormTitle = "Ajouter un revenu récurrent";
    }

    [RelayCommand]
    private async Task SaveGoalAsync()
    {
        if (string.IsNullOrWhiteSpace(GoalName)) { StatusMessage = "Indiquez le nom de l’objectif."; return; }
        if (!AddExpenseViewModel.TryAmount(GoalTargetAmount, out var target) || target <= 0) { StatusMessage = "Indiquez un objectif valide."; return; }
        var saved = 0L;
        if (!string.IsNullOrWhiteSpace(GoalSavedAmount) && (!AddExpenseViewModel.TryAmount(GoalSavedAmount, out saved) || saved < 0)) { StatusMessage = "Indiquez un montant épargné valide."; return; }
        DateTime? targetDate = GoalHasTargetDate ? GoalTargetDate.Date : null;
        if (_editingGoal is null) await finance.AddSavingsGoalAsync(GoalName, target, saved, targetDate);
        else await finance.UpdateSavingsGoalAsync(_editingGoal.Id, GoalName, target, saved, targetDate);
        CancelGoalEdit();
        StatusMessage = "Objectif enregistré.";
        await LoadAsync();
    }

    [RelayCommand]
    private void EditGoal(SavingsGoalRow row)
    {
        _editingGoal = row.Source;
        GoalName = row.Source.Name;
        GoalTargetAmount = AmountText(row.Source.TargetAmountMinor);
        GoalSavedAmount = AmountText(row.Source.SavedAmountMinor);
        GoalHasTargetDate = row.Source.TargetDate is not null;
        GoalTargetDate = row.Source.TargetDate ?? DateTime.Today.AddYears(1);
        GoalFormTitle = "Modifier l’objectif";
    }

    [RelayCommand]
    private async Task DeleteGoalAsync(SavingsGoalRow row)
    {
        await finance.DeleteSavingsGoalAsync(row.Source.Id);
        if (_editingGoal?.Id == row.Source.Id) CancelGoalEdit();
        await LoadAsync();
    }

    [RelayCommand]
    private void CancelGoalEdit()
    {
        _editingGoal = null;
        GoalName = GoalTargetAmount = GoalSavedAmount = string.Empty;
        GoalHasTargetDate = false;
        GoalTargetDate = DateTime.Today.AddYears(1);
        GoalFormTitle = "Ajouter un objectif";
    }

    private string Money(long amount) => DashboardViewModel.Money(amount, settings.CurrencyCode);
    private string HiddenMoney(long amount) => settings.HideAmounts ? "••••" : Money(amount);
    private static string AmountText(long amount) => (amount / 100m).ToString("0.##", CultureInfo.GetCultureInfo("fr-FR"));
    private static string Frequency(int months) => months switch { 3 => "Tous les 3 mois", 12 => "Annuel", _ => "Mensuel" };

    private static bool TrySignedAmount(string value, out long amount)
    {
        if (decimal.TryParse(value.Trim().Replace(',', '.'), NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed))
        {
            amount = checked((long)Math.Round(parsed * 100m, MidpointRounding.AwayFromZero));
            return true;
        }
        amount = 0;
        return false;
    }
}
