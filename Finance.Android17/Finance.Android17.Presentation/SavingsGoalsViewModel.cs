using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Android17.Application;
using Finance.Android17.Domain;

namespace Finance.Android17.Presentation;

/// <summary>
/// Objectifs d'épargne de la page « Revenus » : liste et formulaire. Il est créé par <see cref="IncomeViewModel"/>, qui lui
/// prête son annulation de suppression (<paramref name="offerUndo"/>) et le rechargement de la page (<paramref name="reload"/>).
/// </summary>
public sealed partial class SavingsGoalsViewModel(
    ISavingsGoalRepository goalRepository,
    ISettingsService settings,
    TimeProvider clock,
    Action<string, Func<Task>> offerUndo,
    Func<Task> reload) : StatusViewModelBase
{
    private IReadOnlyList<SavingsGoal> _goals = [];
    private Guid? _editingGoalId;

    public ObservableCollection<SavingsGoalRow> SavingsGoals { get; } = [];

    [NotifyPropertyChangedFor(nameof(IsGoalFormClosed))]
    [ObservableProperty] private bool isGoalFormVisible;
    [ObservableProperty] private string goalName = string.Empty;
    [ObservableProperty] private string goalTargetAmount = string.Empty;
    [ObservableProperty] private string goalSavedAmount = string.Empty;
    [ObservableProperty] private DateTime goalTargetDate = clock.GetLocalNow().Date.AddYears(1);
    [ObservableProperty] private bool goalHasTargetDate;
    [ObservableProperty] private string goalFormTitle = "Ajouter un objectif";

    public bool IsGoalFormClosed => !IsGoalFormVisible;

    public async Task LoadAsync()
    {
        _goals = await goalRepository.GetAllAsync();
        SavingsGoals.Clear();
        foreach (var item in _goals)
            SavingsGoals.Add(new(item.Id, item.Name, Money(item.SavedAmountMinor), Money(item.TargetAmountMinor), MoneyFormatter.Percent(item.Progress), item.Progress));
    }

    [RelayCommand]
    private void ShowGoalForm()
    {
        CancelGoalEdit();
        IsGoalFormVisible = true;
    }

    [RelayCommand]
    private async Task SaveGoalAsync()
    {
        if (string.IsNullOrWhiteSpace(GoalName))
        {
            ShowError("Indiquez le nom de l’objectif.");
            return;
        }
        if (!AmountParser.TryParse(GoalTargetAmount, out var target) || target <= 0)
        {
            ShowError("Indiquez un objectif valide.");
            return;
        }
        var saved = 0L;
        if (!string.IsNullOrWhiteSpace(GoalSavedAmount) && (!AmountParser.TryParse(GoalSavedAmount, out saved) || saved < 0))
        {
            ShowError("Indiquez un montant épargné valide.");
            return;
        }
        DateTime? targetDate = GoalHasTargetDate ? GoalTargetDate.Date : null;
        if (_editingGoalId is not Guid id) await goalRepository.AddAsync(GoalName, target, saved, targetDate);
        else await goalRepository.UpdateAsync(id, GoalName, target, saved, targetDate);
        CancelGoalEdit();
        StatusMessage = "Objectif enregistré.";
        await reload();
    }

    [RelayCommand]
    private void EditGoal(SavingsGoalRow row)
    {
        var goal = _goals.FirstOrDefault(x => x.Id == row.Id);
        if (goal is null) return;
        CancelGoalEdit();
        _editingGoalId = goal.Id;
        GoalName = goal.Name;
        GoalTargetAmount = MoneyFormatter.PlainAmount(goal.TargetAmountMinor);
        GoalSavedAmount = MoneyFormatter.PlainAmount(goal.SavedAmountMinor);
        GoalHasTargetDate = goal.TargetDate is not null;
        GoalTargetDate = goal.TargetDate ?? clock.GetLocalNow().Date.AddYears(1);
        GoalFormTitle = "Modifier l’objectif";
        IsGoalFormVisible = true;
    }

    [RelayCommand]
    private async Task DeleteGoalAsync(SavingsGoalRow row)
    {
        await goalRepository.DeleteAsync(row.Id);
        if (_editingGoalId == row.Id) CancelGoalEdit();
        offerUndo("Objectif supprimé", () => goalRepository.RestoreAsync(row.Id));
        await reload();
    }

    [RelayCommand]
    private void CancelGoalEdit()
    {
        _editingGoalId = null;
        GoalName = GoalTargetAmount = GoalSavedAmount = string.Empty;
        GoalHasTargetDate = false;
        GoalTargetDate = clock.GetLocalNow().Date.AddYears(1);
        GoalFormTitle = "Ajouter un objectif";
        IsGoalFormVisible = false;
    }

    private string Money(long amount) => settings.HideAmounts ? MoneyFormatter.Hidden : MoneyFormatter.Money(amount, settings.CurrencyCode);
}
