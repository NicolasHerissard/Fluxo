using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Android17.Application;
using Finance.Android17.Domain;

namespace Finance.Android17.Presentation;

public partial class ExpensesViewModel(FinanceService finance, ISettingsService settings, TimeProvider clock) : ObservableObject
{
    private const string AllDates = "Toutes les dates";
    private const string CustomPeriodLabel = "Période personnalisée";
    private const string NewestSort = "Plus récentes";
    private const string EmptyListTitle = "Aucune dépense enregistrée";

    private static readonly (string Label, ExpensePeriod Period)[] PeriodOptions =
    [
        (AllDates, ExpensePeriod.AllTime),
        ("Aujourd’hui", ExpensePeriod.Today),
        ("7 derniers jours", ExpensePeriod.Last7Days),
        ("Ce mois", ExpensePeriod.ThisMonth),
        ("Cette année", ExpensePeriod.ThisYear),
        (CustomPeriodLabel, ExpensePeriod.Custom)
    ];

    private static readonly (string Label, ExpenseSort Sort)[] SortOptionList =
    [
        (NewestSort, ExpenseSort.Newest),
        ("Plus anciennes", ExpenseSort.Oldest),
        ("Montant décroissant", ExpenseSort.AmountDescending),
        ("Montant croissant", ExpenseSort.AmountAscending),
        ("Nom A–Z", ExpenseSort.NameAscending),
        ("Nom Z–A", ExpenseSort.NameDescending)
    ];

    /// <summary>Demande d'édition d'une dépense ; porte son identifiant.</summary>
    public event EventHandler<Guid>? EditRequested;
    public ObservableCollection<ExpenseRow> Expenses { get; } = [];
    public ObservableCollection<CategoryFilterOption> CategoryFilters { get; } = [];
    public ObservableCollection<ActiveFilterChip> ActiveFilters { get; } = [];
    public IReadOnlyList<string> Periods { get; } = [.. PeriodOptions.Select(x => x.Label)];
    public IReadOnlyList<string> SortOptions { get; } = [.. SortOptionList.Select(x => x.Label)];
    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string selectedPeriod = AllDates;
    [ObservableProperty] private DateTime customFrom = clock.GetLocalNow().Date.AddMonths(-1);
    [ObservableProperty] private DateTime customTo = clock.GetLocalNow().Date;
    [ObservableProperty] private string minimumAmount = string.Empty;
    [ObservableProperty] private string maximumAmount = string.Empty;
    [ObservableProperty] private string selectedSort = NewestSort;
    [ObservableProperty] private bool isCustomPeriod;
    [ObservableProperty] private bool isFilterPanelVisible;
    [ObservableProperty] private bool hasActiveFilters;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isEmpty;
    [ObservableProperty] private bool isUndoVisible;
    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private string resultSummary = "0 dépense";
    [ObservableProperty] private string filterButtonText = "Filtres";
    [ObservableProperty] private string applyButtonText = "Afficher les résultats";
    [ObservableProperty] private string emptyTitle = EmptyListTitle;
    [ObservableProperty] private string emptySubtitle = "Ajoutez votre première dépense pour commencer.";
    private Guid? _pendingDeletedId;
    private CancellationTokenSource? _searchDelay;
    private IReadOnlyList<Expense> _allExpenses = [];

    partial void OnSearchTextChanged(string value)
    {
        _searchDelay?.Cancel();
        _searchDelay = new();
        UpdateFilterPreview();
        _ = DebouncedRefreshAsync(_searchDelay.Token);
    }

    partial void OnSelectedPeriodChanged(string value) { IsCustomPeriod = value == CustomPeriodLabel; UpdateFilterPreview(); }
    partial void OnCustomFromChanged(DateTime value) => UpdateFilterPreview();
    partial void OnCustomToChanged(DateTime value) => UpdateFilterPreview();
    partial void OnMinimumAmountChanged(string value) => UpdateFilterPreview();
    partial void OnMaximumAmountChanged(string value) => UpdateFilterPreview();

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            if (CategoryFilters.Count == 0)
                foreach (var category in await finance.GetCategoriesAsync()) CategoryFilters.Add(new(category.Id, category.Name));

            _allExpenses = await finance.GetExpensesAsync();
            Expenses.Clear();
            foreach (var expense in ExpenseSearch.Apply(_allExpenses, CurrentCriteria())) Expenses.Add(ExpenseRow.From(expense, settings.HideAmounts));
            IsEmpty = Expenses.Count == 0;
            ResultSummary = $"{Expenses.Count} dépense{(Expenses.Count == 1 ? string.Empty : "s")} · {SelectedSort.ToLowerInvariant()}";
            RefreshActiveFilters();
            var filtered = HasActiveFilters || !string.IsNullOrWhiteSpace(SearchText);
            EmptyTitle = filtered ? "Aucun résultat" : EmptyListTitle;
            EmptySubtitle = filtered ? "Essayez d’élargir votre recherche ou de retirer un filtre." : "Ajoutez votre première dépense pour commencer.";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand] private void OpenFilters() { UpdateFilterPreview(); IsFilterPanelVisible = true; }
    [RelayCommand] private void CloseFilters() => IsFilterPanelVisible = false;
    [RelayCommand] private void ClearSearch() => SearchText = string.Empty;
    [RelayCommand] private void SelectPeriod(string period) => SelectedPeriod = period;
    [RelayCommand] private void ToggleCategory(CategoryFilterOption option) { option.IsSelected = !option.IsSelected; UpdateFilterPreview(); }
    [RelayCommand] private void Edit(ExpenseRow row) => EditRequested?.Invoke(this, row.Id);

    [RelayCommand]
    private async Task ApplyFiltersAsync()
    {
        if (CustomFrom.Date > CustomTo.Date) (CustomFrom, CustomTo) = (CustomTo, CustomFrom);
        IsFilterPanelVisible = false;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ResetFiltersAsync()
    {
        foreach (var option in CategoryFilters) option.IsSelected = false;
        SelectedPeriod = AllDates;
        MinimumAmount = MaximumAmount = string.Empty;
        SelectedSort = NewestSort;
        await ApplyFiltersAsync();
    }

    [RelayCommand]
    private async Task RemoveFilterAsync(ActiveFilterChip chip)
    {
        if (chip.Key.StartsWith("category:", StringComparison.Ordinal) && Guid.TryParse(chip.Key[9..], out var id))
        {
            var option = CategoryFilters.FirstOrDefault(x => x.Id == id);
            if (option is not null) option.IsSelected = false;
        }
        else if (chip.Key == "period") SelectedPeriod = AllDates;
        else if (chip.Key == "minimum") MinimumAmount = string.Empty;
        else if (chip.Key == "maximum") MaximumAmount = string.Empty;
        else if (chip.Key == "sort") SelectedSort = NewestSort;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync(ExpenseRow row)
    {
        await finance.DeleteExpenseAsync(row.Id);
        _pendingDeletedId = row.Id;
        StatusMessage = "Dépense supprimée";
        IsUndoVisible = true;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task UndoDeleteAsync()
    {
        if (_pendingDeletedId is not Guid id) return;
        await finance.RestoreExpenseAsync(id);
        _pendingDeletedId = null;
        IsUndoVisible = false;
        StatusMessage = "Dépense restaurée";
        await LoadAsync();
    }

    private async Task DebouncedRefreshAsync(CancellationToken token)
    {
        try { await Task.Delay(300, token); await LoadAsync(); } catch (OperationCanceledException) { }
    }

    private void RefreshActiveFilters()
    {
        ActiveFilters.Clear();
        foreach (var option in CategoryFilters.Where(x => x.IsSelected)) ActiveFilters.Add(new($"category:{option.Id}", option.Name));
        if (SelectedPeriod != AllDates) ActiveFilters.Add(new("period", SelectedPeriod == CustomPeriodLabel ? $"{CustomFrom:dd/MM} – {CustomTo:dd/MM}" : SelectedPeriod));
        if (AmountParser.TryParse(MinimumAmount, out _)) ActiveFilters.Add(new("minimum", $"Min. {MinimumAmount} {settings.CurrencyCode}"));
        if (AmountParser.TryParse(MaximumAmount, out _)) ActiveFilters.Add(new("maximum", $"Max. {MaximumAmount} {settings.CurrencyCode}"));
        if (SelectedSort != NewestSort) ActiveFilters.Add(new("sort", SelectedSort));
        HasActiveFilters = ActiveFilters.Count > 0;
        FilterButtonText = HasActiveFilters ? $"Filtres ({ActiveFilters.Count})" : "Filtres";
    }

    private void UpdateFilterPreview()
    {
        if (_allExpenses.Count == 0) { ApplyButtonText = "Afficher 0 résultat"; return; }
        var count = ExpenseSearch.Count(_allExpenses, CurrentCriteria());
        ApplyButtonText = $"Afficher {count} résultat{(count == 1 ? string.Empty : "s")}";
    }

    private ExpenseCriteria CurrentCriteria()
    {
        var (from, to) = ExpensePeriods.Resolve(PeriodOptions.FirstOrDefault(x => x.Label == SelectedPeriod).Period, clock.GetLocalNow().Date, CustomFrom, CustomTo);
        return new ExpenseCriteria(
            SearchText,
            CategoryFilters.Where(x => x.IsSelected).Select(x => x.Id).ToHashSet(),
            from, to,
            AmountParser.TryParse(MinimumAmount, out var minimum) ? minimum : null,
            AmountParser.TryParse(MaximumAmount, out var maximum) ? maximum : null,
            SortOptionList.FirstOrDefault(x => x.Label == SelectedSort).Sort);
    }
}
