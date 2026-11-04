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
    FinanceDataLoader dataLoader,
    ISettingsService settings,
    TimeProvider clock) : UndoableViewModelBase
{
    private SavingsGoalsViewModel? _goals;
    private IReadOnlyList<Income> _incomes = [];
    private IReadOnlyList<RecurringIncome> _recurringIncomes = [];
    private Guid? _editingIncomeId;
    private Guid? _editingRecurringIncomeId;

    public ObservableCollection<IncomeRow> Incomes { get; } = [];
    public ObservableCollection<RecurringIncomeRow> RecurringIncomes { get; } = [];

    /// <summary>Objectifs d'épargne de la page : liste et formulaire propres, annulation de suppression partagée avec cette page.</summary>
    public SavingsGoalsViewModel Goals => _goals ??= new(goalRepository, settings, clock, OfferUndo, LoadAsync);
    public IReadOnlyList<string> Frequencies { get; } = BillingFrequency.Options;

    [ObservableProperty] private string currentBalance = "0,00 €";
    [ObservableProperty] private string monthIncome = "0,00 €";
    [ObservableProperty] private string monthNet = "0,00 €";
    [ObservableProperty] private string forecastBalance = "0,00 €";
    [ObservableProperty] private string expectedIncome = "0,00 €";
    [ObservableProperty] private string incomeDescription = string.Empty;
    [ObservableProperty] private string incomeAmount = string.Empty;
    [ObservableProperty] private DateTime incomeDate = clock.GetLocalNow().Date;
    [ObservableProperty] private string incomeFormTitle = "Ajouter un revenu";
    [ObservableProperty] private string selectedFrequency = BillingFrequency.Monthly;
    [NotifyPropertyChangedFor(nameof(IncomeDateLabel))]
    [ObservableProperty] private bool incomeRepeats;
    [NotifyPropertyChangedFor(nameof(IsIncomeFormClosed))]
    [ObservableProperty] private bool isIncomeFormVisible;

    public string IncomeDateLabel => IncomeRepeats ? "Date de la prochaine échéance" : "Date du revenu";
    public bool IsIncomeFormClosed => !IsIncomeFormVisible;
    /// <summary>On ne transforme pas un revenu en revenu récurrent (ni l'inverse) à la modification : l'option « Se répète » n'est offerte qu'à la création.</summary>
    public bool CanChooseRepeat => _editingIncomeId is null && _editingRecurringIncomeId is null;

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
            await Goals.LoadAsync();

            Incomes.Clear();
            foreach (var item in _incomes)
                Incomes.Add(new(item.Id, item.Description, MoneyFormatter.Date(item.OccurredOn), HiddenMoney(item.AmountMinor)));

            RecurringIncomes.Clear();
            foreach (var item in _recurringIncomes)
                RecurringIncomes.Add(new(item.Id, item.Name, HiddenMoney(item.AmountMinor), BillingFrequency.ToDisplay(item.IntervalMonths), MoneyFormatter.Date(item.NextOccurrenceDate)));

            var summary = FinancialSummaryCalculator.Build(data, clock.GetLocalNow().Date);
            CurrentBalance = HiddenMoney(summary.CurrentBalanceMinor);
            MonthIncome = HiddenMoney(summary.IncomeTotalMinor);
            MonthNet = HiddenMoney(summary.NetTotalMinor);
            ForecastBalance = HiddenMoney(summary.ForecastBalanceMinor);
            ExpectedIncome = HiddenMoney(ExpectedRestOfMonth(_recurringIncomes, clock.GetLocalNow().Date));
        }
        finally { IsBusy = false; }
    }

    /// <summary>Revenus récurrents encore attendus d'ici la fin du mois (après aujourd'hui), comme dans la prévision de fin de mois.</summary>
    private static long ExpectedRestOfMonth(IEnumerable<RecurringIncome> recurring, DateTime today)
    {
        var monthEnd = new DateTime(today.Year, today.Month, 1).AddMonths(1).AddDays(-1);
        return recurring.Where(x => x.DeletedAtUtc is null && x.NextOccurrenceDate.Date > today.Date && x.NextOccurrenceDate.Date <= monthEnd).Sum(x => x.AmountMinor);
    }

    [RelayCommand]
    private void ShowIncomeForm()
    {
        CancelIncomeEdit();
        IsIncomeFormVisible = true;
    }

    [RelayCommand]
    private async Task SaveIncomeAsync()
    {
        if (string.IsNullOrWhiteSpace(IncomeDescription)) { ShowError("Indiquez l’origine du revenu."); return; }
        if (!AmountParser.TryParse(IncomeAmount, out var amount) || amount <= 0) { ShowError("Indiquez un montant valide."); return; }
        var recurring = IncomeRepeats || _editingRecurringIncomeId is not null;
        if (recurring)
        {
            var interval = BillingFrequency.ToMonths(SelectedFrequency);
            if (_editingRecurringIncomeId is not Guid recurringId) await recurringIncomeRepository.AddAsync(IncomeDescription, amount, interval, IncomeDate, settings.CurrencyCode);
            else await recurringIncomeRepository.UpdateAsync(recurringId, IncomeDescription, amount, interval, IncomeDate, settings.CurrencyCode);
        }
        else if (_editingIncomeId is not Guid id) await incomeRepository.AddAsync(amount, IncomeDescription, IncomeDate, settings.CurrencyCode);
        else await incomeRepository.UpdateAsync(id, amount, IncomeDescription, IncomeDate, settings.CurrencyCode);
        CancelIncomeEdit();
        StatusMessage = recurring ? "Revenu récurrent enregistré." : "Revenu enregistré.";
        await LoadAsync();
    }

    [RelayCommand]
    private void EditIncome(IncomeRow row)
    {
        var income = _incomes.FirstOrDefault(x => x.Id == row.Id);
        if (income is null) return;
        CancelIncomeEdit();
        _editingIncomeId = income.Id;
        IncomeDescription = income.Description;
        IncomeAmount = MoneyFormatter.PlainAmount(income.AmountMinor);
        IncomeDate = income.OccurredOn;
        IncomeFormTitle = "Modifier le revenu";
        IsIncomeFormVisible = true;
        OnPropertyChanged(nameof(CanChooseRepeat));
    }

    protected override Task ReloadAfterUndoAsync() => LoadAsync();

    [RelayCommand]
    private async Task DeleteIncomeAsync(IncomeRow row)
    {
        await incomeRepository.DeleteAsync(row.Id);
        if (_editingIncomeId == row.Id) CancelIncomeEdit();
        OfferUndo("Revenu supprimé", () => incomeRepository.RestoreAsync(row.Id));
        await LoadAsync();
    }

    [RelayCommand]
    private void CancelIncomeEdit()
    {
        _editingIncomeId = null;
        _editingRecurringIncomeId = null;
        IncomeDescription = IncomeAmount = string.Empty;
        IncomeDate = clock.GetLocalNow().Date;
        IncomeRepeats = false;
        SelectedFrequency = BillingFrequency.Monthly;
        IncomeFormTitle = "Ajouter un revenu";
        IsIncomeFormVisible = false;
        OnPropertyChanged(nameof(CanChooseRepeat));
    }

    [RelayCommand]
    private void EditRecurringIncome(RecurringIncomeRow row)
    {
        var recurring = _recurringIncomes.FirstOrDefault(x => x.Id == row.Id);
        if (recurring is null) return;
        CancelIncomeEdit();
        _editingRecurringIncomeId = recurring.Id;
        IncomeRepeats = true;
        IncomeDescription = recurring.Name;
        IncomeAmount = MoneyFormatter.PlainAmount(recurring.AmountMinor);
        SelectedFrequency = BillingFrequency.ToOption(recurring.IntervalMonths);
        IncomeDate = recurring.NextOccurrenceDate;
        IncomeFormTitle = "Modifier le revenu récurrent";
        IsIncomeFormVisible = true;
        OnPropertyChanged(nameof(CanChooseRepeat));
    }

    [RelayCommand]
    private async Task DeleteRecurringIncomeAsync(RecurringIncomeRow row)
    {
        await recurringIncomeRepository.DeleteAsync(row.Id);
        if (_editingRecurringIncomeId == row.Id) CancelIncomeEdit();
        OfferUndo("Revenu récurrent supprimé", () => recurringIncomeRepository.RestoreAsync(row.Id));
        await LoadAsync();
    }

    private string Money(long amount) => MoneyFormatter.Money(amount, settings.CurrencyCode);
    private string HiddenMoney(long amount) => settings.HideAmounts ? MoneyFormatter.Hidden : Money(amount);
}
