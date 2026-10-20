using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Android17.Application;
using Finance.Android17.Domain;

namespace Finance.Android17.Presentation;

public sealed record ExpenseRow(Guid Id, string Description, string Category, string Date, string Amount, string CategoryColor, string CategoryInitial, Expense Source);
public sealed record ChartBar(string Label, string Amount, string Percent, double Ratio, string Color, bool HasBudget, string BudgetText, double BudgetProgress, string BudgetColor);
public sealed record SubscriptionRow(string Name, string Amount, string Frequency, string NextDate, Subscription Source);
public sealed record ActiveFilterChip(string Key, string Label);

public partial class CategoryFilterOption : ObservableObject
{
    public CategoryFilterOption(Category category) => Category = category;
    public Category Category { get; }
    public string Name => Category.Name;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    [NotifyPropertyChangedFor(nameof(AccessibilityDescription))]
    private bool isSelected;
    public string DisplayName => IsSelected ? $"✓ {Name}" : Name;
    public string AccessibilityDescription => IsSelected ? $"{Name}, sélectionnée" : $"{Name}, non sélectionnée";
}

public partial class ExpenseCategoryOption(Category category) : ObservableObject
{
    public Category Category { get; } = category;
    public string Name => Category.Name;
    public string Color => Category.Color;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionStroke))]
    [NotifyPropertyChangedFor(nameof(SelectionStrokeThickness))]
    [NotifyPropertyChangedFor(nameof(SelectionMark))]
    private bool isSelected;
    public string SelectionStroke => IsSelected ? "#D0BCFF" : "Transparent";
    public double SelectionStrokeThickness => IsSelected ? 3 : 0;
    public string SelectionMark => IsSelected ? "✓" : string.Empty;
}

public partial class LockViewModel(FinanceService finance, IAppLockService appLock, IBiometricService biometrics, ISettingsService settings, IReminderService reminders, TimeProvider clock) : ObservableObject
{
    private bool _initialized;
    public event EventHandler? Unlocked;

    [ObservableProperty] private bool isFirstRun;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isPinHidden = true;
    [ObservableProperty] private bool canUseBiometrics;
    [ObservableProperty] private string pin = string.Empty;
    [ObservableProperty] private string pinTitle = "Déverrouillage";
    [ObservableProperty] private string pinHelp = "Saisissez votre code PIN.";
    [ObservableProperty] private string pinDots = "○ ○ ○ ○";
    [ObservableProperty] private string pinVisibilityIcon = "icon_visibility.png";
    [ObservableProperty] private string currencySymbol = "€";
    [ObservableProperty] private string errorMessage = string.Empty;

    partial void OnPinChanged(string value)
    {
        var count = Math.Max(4, value.Length);
        PinDots = string.Join(" ", Enumerable.Range(0, count).Select(i => i < value.Length ? "●" : "○"));
    }

    partial void OnIsPinHiddenChanged(bool value) => PinVisibilityIcon = value ? "icon_visibility.png" : "icon_visibility_off.png";

    [RelayCommand]
    public async Task InitializeAsync()
    {
        CurrencySymbol = DashboardViewModel.CurrencySymbol(settings.CurrencyCode);
        if (_initialized) return;
        _initialized = true;
        IsFirstRun = !await appLock.HasPinAsync();
        PinTitle = IsFirstRun ? "Créer votre code PIN" : "Bon retour";
        PinHelp = IsFirstRun ? "Choisissez 4 à 8 chiffres pour protéger vos données." : "Déverrouillez vos finances personnelles.";
        CanUseBiometrics = !IsFirstRun && settings.BiometricsEnabled && biometrics.IsAvailable;
    }

    [RelayCommand]
    private async Task SubmitPinAsync()
    {
        if (IsBusy) return;
        ErrorMessage = string.Empty;
        if (Pin.Length is < 4 or > 8 || Pin.Any(x => !char.IsDigit(x)))
        {
            ErrorMessage = "Le PIN doit contenir entre 4 et 8 chiffres.";
            return;
        }

        IsBusy = true;
        try
        {
            if (IsFirstRun) await appLock.SetPinAsync(Pin);
            else if (!await appLock.VerifyPinAsync(Pin))
            {
                ErrorMessage = "Code PIN incorrect. Réessayez.";
                Pin = string.Empty;
                return;
            }
            await PrepareFinanceAsync();
            Pin = string.Empty;
            Unlocked?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) { ErrorMessage = $"Ouverture impossible : {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task UseBiometricsAsync()
    {
        if (!CanUseBiometrics || IsBusy) return;
        IsBusy = true;
        try
        {
            if (await biometrics.AuthenticateAsync("Déverrouiller Mes Finances"))
            {
                await PrepareFinanceAsync();
                Unlocked?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex) { ErrorMessage = $"Biométrie indisponible : {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand] private void TogglePinVisibility() => IsPinHidden = !IsPinHidden;

    private async Task PrepareFinanceAsync()
    {
        await finance.InitializeAsync();
        if (settings.AutoCreateSubscriptionExpenses)
            await finance.ProcessDueSubscriptionsAsync(clock.GetLocalNow().Date, settings.CurrencyCode);
        if (settings.AutoCreateRecurringIncomes)
            await finance.ProcessDueRecurringIncomesAsync(clock.GetLocalNow().Date, settings.CurrencyCode);
        if (settings.SubscriptionRemindersEnabled)
            await reminders.ShowSubscriptionReminderAsync(await finance.GetSubscriptionsAsync(), settings.SubscriptionReminderDays);
    }

    public void Reset()
    {
        _initialized = false;
        Pin = ErrorMessage = string.Empty;
        IsBusy = false;
        _ = InitializeAsync();
    }
}

public partial class DashboardViewModel(FinanceService finance, ISettingsService settings, TimeProvider clock) : ObservableObject
{
    private bool _hasLoaded;
    public ObservableCollection<ExpenseRow> RecentExpenses { get; } = [];
    public ObservableCollection<ChartBar> CategoryBars { get; } = [];
    [ObservableProperty] private string selectedPeriod = "Mois";
    [ObservableProperty] private string greeting = "Bonjour";
    [ObservableProperty] private string currentDate = string.Empty;
    [ObservableProperty] private string periodTitle = "Ce mois";
    [ObservableProperty] private string total = "0,00 €";
    [ObservableProperty] private string comparison = "Pas encore de comparaison";
    [ObservableProperty] private string comparisonColor = "#E9DDFF";
    [ObservableProperty] private string expenseCount = "0 dépense";
    [ObservableProperty] private string topCategory = "—";
    [ObservableProperty] private string nextSubscription = "Aucun abonnement";
    [ObservableProperty] private string currentBalance = "0,00 €";
    [ObservableProperty] private string monthlyIncome = "0,00 €";
    [ObservableProperty] private string monthlyNet = "0,00 €";
    [ObservableProperty] private string forecastBalance = "0,00 €";
    [ObservableProperty] private string forecastStatus = "Après échéances connues";
    [ObservableProperty] private string savingsRate = "0 %";
    [ObservableProperty] private string budgetStatus = string.Empty;
    [ObservableProperty] private double budgetProgress;
    [ObservableProperty] private bool isBudgetVisible;
    [ObservableProperty] private string budgetColor = "#81C995";
    [ObservableProperty] private bool isEmpty;
    [ObservableProperty] private bool hasData;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isSkeletonVisible = true;
    [ObservableProperty] private bool isDashboardVisible;
    [ObservableProperty] private string privacyIcon = "icon_visibility.png";
    [ObservableProperty] private string weekBackground = "Transparent";
    [ObservableProperty] private string monthBackground = "#6750A4";
    [ObservableProperty] private string yearBackground = "Transparent";
    [ObservableProperty] private string weekTextColor = "#CAC4D0";
    [ObservableProperty] private string monthTextColor = "#FFFFFF";
    [ObservableProperty] private string yearTextColor = "#CAC4D0";

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        IsSkeletonVisible = !_hasLoaded;
        IsDashboardVisible = _hasLoaded;
        try
        {
            var culture = CultureInfo.GetCultureInfo("fr-FR");
            Greeting = clock.GetLocalNow().Hour < 18 ? "Bonjour" : "Bonsoir";
            CurrentDate = culture.TextInfo.ToTitleCase(clock.GetLocalNow().Date.ToString("dddd d MMMM", culture));
            var all = await finance.GetExpensesAsync();
            var incomes = await finance.GetIncomesAsync();
            var subscriptions = await finance.GetSubscriptionsAsync();
            var recurringIncomes = await finance.GetRecurringIncomesAsync();
            var initialBalance = await finance.GetInitialBalanceAsync();
            var financial = FinanceService.BuildFinancialSummary(all, incomes, initialBalance, subscriptions, recurringIncomes, clock.GetLocalNow().Date);
            CurrentBalance = settings.HideAmounts ? "••••" : Money(financial.CurrentBalanceMinor, settings.CurrencyCode);
            MonthlyIncome = settings.HideAmounts ? "••••" : Money(financial.IncomeTotalMinor, settings.CurrencyCode);
            MonthlyNet = settings.HideAmounts ? "••••" : Money(financial.NetTotalMinor, settings.CurrencyCode);
            ForecastBalance = settings.HideAmounts ? "••••" : Money(financial.ForecastBalanceMinor, settings.CurrencyCode);
            ForecastStatus = financial.ForecastBalanceMinor < 0 ? "Découvert probable en fin de mois" : "Après échéances connues";
            SavingsRate = $"{financial.SavingsRate:P0}";
            var (from, to, previousFrom, previousTo, title) = GetRanges(SelectedPeriod, clock.GetLocalNow().Date);
            PeriodTitle = title;
            var current = all.Where(x => x.OccurredOn.Date >= from && x.OccurredOn.Date <= to).ToList();
            var previous = all.Where(x => x.OccurredOn.Date >= previousFrom && x.OccurredOn.Date <= previousTo).ToList();
            var currentTotal = current.Sum(x => x.AmountMinor);
            var previousTotal = previous.Sum(x => x.AmountMinor);
            Total = settings.HideAmounts ? "••••••" : Money(currentTotal, settings.CurrencyCode);
            PrivacyIcon = settings.HideAmounts ? "icon_visibility_off.png" : "icon_visibility.png";
            ExpenseCount = $"{current.Count} dépense{(current.Count > 1 ? "s" : string.Empty)}";
            var next = subscriptions.Where(x => x.NextBillingDate.Date >= clock.GetLocalNow().Date).OrderBy(x => x.NextBillingDate).FirstOrDefault();
            NextSubscription = next is null ? "Aucun abonnement" : $"{next.Name} · {next.NextBillingDate.ToString("dd MMM", culture)}";
            var groups = current.GroupBy(x => new { Name = x.Category?.Name ?? "Sans catégorie", Color = x.Category?.Color ?? "#6750A4", Budget = x.Category?.BudgetMinor })
                .Select(g => new { g.Key.Name, g.Key.Color, g.Key.Budget, Total = g.Sum(x => x.AmountMinor) }).OrderByDescending(x => x.Total).ToList();
            TopCategory = groups.FirstOrDefault()?.Name ?? "—";
            CategoryBars.Clear();
            var distributionTotal = Math.Max(1, currentTotal);
            foreach (var group in groups.Take(3))
            {
                var ratio = (double)group.Total / distributionTotal;
                var hasBudget = group.Budget is > 0;
                var budgetProgress = hasBudget ? Math.Clamp((double)group.Total / group.Budget!.Value, 0, 1) : 0;
                var budgetText = hasBudget ? $"Budget : {Money(group.Total, settings.CurrencyCode)} / {Money(group.Budget!.Value, settings.CurrencyCode)}" : string.Empty;
                var budgetColor = budgetProgress >= 1 ? "#FFB4AB" : budgetProgress >= .8 ? "#FFD8A8" : group.Color;
                CategoryBars.Add(new ChartBar(group.Name, settings.HideAmounts ? "••••" : Money(group.Total, settings.CurrencyCode), $"{ratio:P0}", ratio, group.Color, hasBudget, budgetText, budgetProgress, budgetColor));
            }
            RecentExpenses.Clear();
            foreach (var expense in all.OrderByDescending(x => x.OccurredOn).ThenByDescending(x => x.CreatedAtUtc).Take(3))
                RecentExpenses.Add(ToRow(expense, settings.HideAmounts));
            IsEmpty = current.Count == 0;
            HasData = !IsEmpty;
            IsBudgetVisible = settings.MonthlyBudgetMinor > 0 && SelectedPeriod == "Mois";
            if (IsBudgetVisible)
            {
                BudgetProgress = Math.Clamp((double)currentTotal / settings.MonthlyBudgetMinor, 0, 1);
                var remaining = settings.MonthlyBudgetMinor - currentTotal;
                BudgetStatus = remaining >= 0 ? $"{Money(remaining, settings.CurrencyCode)} disponibles sur le budget" : $"Budget dépassé de {Money(-remaining, settings.CurrencyCode)}";
                BudgetColor = BudgetProgress >= 1 ? "#FFB4AB" : BudgetProgress >= .8 ? "#FFD8A8" : "#B7F5C5";
            }
            if (previousTotal == 0)
            {
                Comparison = "Aucune donnée sur la période précédente";
                ComparisonColor = "#E9DDFF";
            }
            else
            {
                var difference = currentTotal - previousTotal;
                var percentage = Math.Abs(difference * 100d / previousTotal);
                Comparison = difference switch
                {
                    < 0 => $"↓ {percentage:0} % par rapport à la période précédente",
                    > 0 => $"↑ {percentage:0} % par rapport à la période précédente",
                    _ => "→ Identique à la période précédente"
                };
                ComparisonColor = difference < 0 ? "#B7F5C5" : difference > 0 ? "#FFDAD6" : "#E9DDFF";
            }
        }
        finally
        {
            _hasLoaded = true;
            IsSkeletonVisible = false;
            IsDashboardVisible = true;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SelectPeriodAsync(string period)
    {
        SelectedPeriod = period;
        WeekBackground = period == "Semaine" ? "#6750A4" : "Transparent";
        MonthBackground = period == "Mois" ? "#6750A4" : "Transparent";
        YearBackground = period == "Année" ? "#6750A4" : "Transparent";
        WeekTextColor = period == "Semaine" ? "#FFFFFF" : "#CAC4D0";
        MonthTextColor = period == "Mois" ? "#FFFFFF" : "#CAC4D0";
        YearTextColor = period == "Année" ? "#FFFFFF" : "#CAC4D0";
        await LoadAsync();
    }
    [RelayCommand] private async Task TogglePrivacyAsync() { settings.HideAmounts = !settings.HideAmounts; await LoadAsync(); }

    internal static ExpenseRow ToRow(Expense x, bool hide)
    {
        var category = x.Category?.Name ?? "Sans catégorie";
        return new(x.Id, string.IsNullOrWhiteSpace(x.Description) ? "Dépense" : x.Description, category,
            x.OccurredOn.ToString("dd MMM yyyy", CultureInfo.GetCultureInfo("fr-FR")), hide ? "••••" : Money(x.AmountMinor, x.CurrencyCode),
            x.Category?.Color ?? "#6750A4", category[..1].ToUpperInvariant(), x);
    }
    internal static string Money(long minor, string currencyCode = "EUR")
    {
        return $"{(minor / 100m).ToString("N2", CultureInfo.GetCultureInfo("fr-FR"))} {CurrencySymbol(currencyCode)}";
    }

    internal static string CurrencySymbol(string currencyCode) => currencyCode.ToUpperInvariant() switch
    {
        "USD" => "$",
        "GBP" => "£",
        "CHF" => "CHF",
        "CAD" => "CA$",
        "JPY" => "¥",
        _ => "€"
    };

    private static (DateTime from, DateTime to, DateTime previousFrom, DateTime previousTo, string title) GetRanges(string period, DateTime today) => period switch
    {
        "Semaine" => (today.AddDays(-((int)today.DayOfWeek + 6) % 7).Date, today.Date, today.AddDays(-((int)today.DayOfWeek + 6) % 7 - 7).Date, today.AddDays(-((int)today.DayOfWeek + 6) % 7 - 1).Date, "Cette semaine"),
        "Année" => (new(today.Year, 1, 1), today.Date, new(today.Year - 1, 1, 1), new(today.Year - 1, 12, 31), "Cette année"),
        _ => (new(today.Year, today.Month, 1), today.Date, new DateTime(today.Year, today.Month, 1).AddMonths(-1), new DateTime(today.Year, today.Month, 1).AddDays(-1), "Ce mois")
    };
}

public partial class ExpensesViewModel(FinanceService finance, ISettingsService settings, TimeProvider clock) : ObservableObject
{
    public event EventHandler<Expense>? EditRequested;
    public ObservableCollection<ExpenseRow> Expenses { get; } = [];
    public ObservableCollection<CategoryFilterOption> CategoryFilters { get; } = [];
    public ObservableCollection<ActiveFilterChip> ActiveFilters { get; } = [];
    public IReadOnlyList<string> Periods { get; } = ["Toutes les dates", "Aujourd’hui", "7 derniers jours", "Ce mois", "Cette année", "Période personnalisée"];
    public IReadOnlyList<string> SortOptions { get; } = ["Plus récentes", "Plus anciennes", "Montant décroissant", "Montant croissant", "Nom A–Z", "Nom Z–A"];
    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string selectedPeriod = "Toutes les dates";
    [ObservableProperty] private DateTime customFrom = clock.GetLocalNow().Date.AddMonths(-1);
    [ObservableProperty] private DateTime customTo = clock.GetLocalNow().Date;
    [ObservableProperty] private string minimumAmount = string.Empty;
    [ObservableProperty] private string maximumAmount = string.Empty;
    [ObservableProperty] private string selectedSort = "Plus récentes";
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
    [ObservableProperty] private string emptyTitle = "Aucune dépense enregistrée";
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

    partial void OnSelectedPeriodChanged(string value) { IsCustomPeriod = value == "Période personnalisée"; UpdateFilterPreview(); }
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
                foreach (var category in await finance.GetCategoriesAsync()) CategoryFilters.Add(new(category));

            _allExpenses = await finance.GetExpensesAsync();
            IEnumerable<Expense> list = _allExpenses;
            var selectedIds = CategoryFilters.Where(x => x.IsSelected).Select(x => x.Category.Id).ToHashSet();
            if (selectedIds.Count > 0) list = list.Where(x => selectedIds.Contains(x.CategoryId));

            var (from, to) = Range(SelectedPeriod, CustomFrom, CustomTo);
            if (from is DateTime fromDate) list = list.Where(x => x.OccurredOn.Date >= fromDate.Date);
            if (to is DateTime toDate) list = list.Where(x => x.OccurredOn.Date <= toDate.Date);
            if (TryOptionalAmount(MinimumAmount, out var minimum)) list = list.Where(x => x.AmountMinor >= minimum);
            if (TryOptionalAmount(MaximumAmount, out var maximum)) list = list.Where(x => x.AmountMinor <= maximum);
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var terms = Normalize(SearchText).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                list = list.Where(x => terms.All(term => SearchableText(x).Contains(term, StringComparison.Ordinal)));
            }

            list = SelectedSort switch
            {
                "Plus anciennes" => list.OrderBy(x => x.OccurredOn).ThenBy(x => x.CreatedAtUtc),
                "Montant décroissant" => list.OrderByDescending(x => x.AmountMinor).ThenByDescending(x => x.OccurredOn),
                "Montant croissant" => list.OrderBy(x => x.AmountMinor).ThenByDescending(x => x.OccurredOn),
                "Nom A–Z" => list.OrderBy(x => x.Description, StringComparer.CurrentCultureIgnoreCase),
                "Nom Z–A" => list.OrderByDescending(x => x.Description, StringComparer.CurrentCultureIgnoreCase),
                _ => list.OrderByDescending(x => x.OccurredOn).ThenByDescending(x => x.CreatedAtUtc)
            };

            Expenses.Clear();
            foreach (var expense in list) Expenses.Add(DashboardViewModel.ToRow(expense, settings.HideAmounts));
            IsEmpty = Expenses.Count == 0;
            ResultSummary = $"{Expenses.Count} dépense{(Expenses.Count == 1 ? string.Empty : "s")} · {SelectedSort.ToLowerInvariant()}";
            RefreshActiveFilters();
            var filtered = HasActiveFilters || !string.IsNullOrWhiteSpace(SearchText);
            EmptyTitle = filtered ? "Aucun résultat" : "Aucune dépense enregistrée";
            EmptySubtitle = filtered ? "Essayez d’élargir votre recherche ou de retirer un filtre." : "Ajoutez votre première dépense pour commencer.";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand] private void OpenFilters() { UpdateFilterPreview(); IsFilterPanelVisible = true; }
    [RelayCommand] private void CloseFilters() => IsFilterPanelVisible = false;
    [RelayCommand] private void ClearSearch() => SearchText = string.Empty;
    [RelayCommand] private void SelectPeriod(string period) => SelectedPeriod = period;
    [RelayCommand] private void ToggleCategory(CategoryFilterOption option) { option.IsSelected = !option.IsSelected; UpdateFilterPreview(); }
    [RelayCommand] private void Edit(ExpenseRow row) => EditRequested?.Invoke(this, row.Source);

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
        SelectedPeriod = "Toutes les dates";
        MinimumAmount = MaximumAmount = string.Empty;
        SelectedSort = "Plus récentes";
        await ApplyFiltersAsync();
    }

    [RelayCommand]
    private async Task RemoveFilterAsync(ActiveFilterChip chip)
    {
        if (chip.Key.StartsWith("category:", StringComparison.Ordinal) && Guid.TryParse(chip.Key[9..], out var id))
        {
            var option = CategoryFilters.FirstOrDefault(x => x.Category.Id == id);
            if (option is not null) option.IsSelected = false;
        }
        else if (chip.Key == "period") SelectedPeriod = "Toutes les dates";
        else if (chip.Key == "minimum") MinimumAmount = string.Empty;
        else if (chip.Key == "maximum") MaximumAmount = string.Empty;
        else if (chip.Key == "sort") SelectedSort = "Plus récentes";
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
        foreach (var option in CategoryFilters.Where(x => x.IsSelected)) ActiveFilters.Add(new($"category:{option.Category.Id}", option.Name));
        if (SelectedPeriod != "Toutes les dates") ActiveFilters.Add(new("period", SelectedPeriod == "Période personnalisée" ? $"{CustomFrom:dd/MM} – {CustomTo:dd/MM}" : SelectedPeriod));
        if (TryOptionalAmount(MinimumAmount, out _)) ActiveFilters.Add(new("minimum", $"Min. {MinimumAmount} {settings.CurrencyCode}"));
        if (TryOptionalAmount(MaximumAmount, out _)) ActiveFilters.Add(new("maximum", $"Max. {MaximumAmount} {settings.CurrencyCode}"));
        if (SelectedSort != "Plus récentes") ActiveFilters.Add(new("sort", SelectedSort));
        HasActiveFilters = ActiveFilters.Count > 0;
        FilterButtonText = HasActiveFilters ? $"Filtres ({ActiveFilters.Count})" : "Filtres";
    }

    private void UpdateFilterPreview()
    {
        if (_allExpenses.Count == 0) { ApplyButtonText = "Afficher 0 résultat"; return; }
        IEnumerable<Expense> list = _allExpenses;
        var selectedIds = CategoryFilters.Where(x => x.IsSelected).Select(x => x.Category.Id).ToHashSet();
        if (selectedIds.Count > 0) list = list.Where(x => selectedIds.Contains(x.CategoryId));
        var (from, to) = Range(SelectedPeriod, CustomFrom, CustomTo);
        if (from is DateTime fromDate) list = list.Where(x => x.OccurredOn.Date >= fromDate.Date);
        if (to is DateTime toDate) list = list.Where(x => x.OccurredOn.Date <= toDate.Date);
        if (TryOptionalAmount(MinimumAmount, out var minimum)) list = list.Where(x => x.AmountMinor >= minimum);
        if (TryOptionalAmount(MaximumAmount, out var maximum)) list = list.Where(x => x.AmountMinor <= maximum);
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var terms = Normalize(SearchText).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            list = list.Where(x => terms.All(term => SearchableText(x).Contains(term, StringComparison.Ordinal)));
        }
        var count = list.Count();
        ApplyButtonText = $"Afficher {count} résultat{(count == 1 ? string.Empty : "s")}";
    }

    private static bool TryOptionalAmount(string value, out long minor)
    {
        minor = 0;
        return !string.IsNullOrWhiteSpace(value) && AddExpenseViewModel.TryAmount(value, out minor);
    }

    private static string SearchableText(Expense expense)
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var amount = (expense.AmountMinor / 100m).ToString("0.00", culture);
        return Normalize($"{expense.Description} {expense.Category?.Name} {amount} {expense.AmountMinor} {expense.OccurredOn.ToString("dd/MM/yyyy", culture)} {expense.OccurredOn.ToString("dd MMMM yyyy", culture)}");
    }

    private static string Normalize(string value)
    {
        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return string.Concat(decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).Normalize(NormalizationForm.FormC);
    }

    private (DateTime? from, DateTime? to) Range(string value, DateTime customFrom, DateTime customTo)
    {
        var today = clock.GetLocalNow().Date;
        return value switch
        {
            "Aujourd’hui" => (today, today),
            "7 derniers jours" => (today.AddDays(-6), today),
            "Ce mois" => (new DateTime(today.Year, today.Month, 1), today),
            "Cette année" => (new DateTime(today.Year, 1, 1), today),
            "Période personnalisée" => (customFrom.Date, customTo.Date),
            _ => (null, null)
        };
    }
}

public partial class AddExpenseViewModel(FinanceService finance, ISettingsService settings, IOcrService ocr, TimeProvider clock) : ObservableObject
{
    public ObservableCollection<Category> Categories { get; } = [];
    public ObservableCollection<ExpenseCategoryOption> CategoryOptions { get; } = [];
    public event EventHandler? Saved;
    [ObservableProperty] private string amount = string.Empty;
    [ObservableProperty] private string currencySymbol = "€";
    [ObservableProperty] private string description = string.Empty;
    [ObservableProperty] private DateTime date = clock.GetLocalNow().Date;
    [ObservableProperty] private Category? selectedCategory;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isSaveEnabled = true;
    [ObservableProperty] private bool isCustomDateVisible;
    [ObservableProperty] private string errorMessage = string.Empty;
    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private string pageTitle = "Nouvelle dépense";
    [ObservableProperty] private string saveButtonText = "Ajouter la dépense";
    [ObservableProperty] private bool isTodaySelected = true;
    [ObservableProperty] private bool isYesterdaySelected;
    [ObservableProperty] private bool isOtherDateSelected;
    [ObservableProperty] private string otherDateText = "Autre";
    private Guid? _editingId;

    partial void OnIsBusyChanged(bool value) => IsSaveEnabled = !value;
    partial void OnAmountChanged(string value) => UpdateSaveButtonText();
    partial void OnDateChanged(DateTime value) => UpdateDateButtons();
    partial void OnSelectedCategoryChanged(Category? value)
    {
        if (value is not null && settings.RememberLastCategory) settings.LastCategoryId = value.Id.ToString();
        foreach (var option in CategoryOptions) option.IsSelected = option.Category.Id == value?.Id;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        CurrencySymbol = DashboardViewModel.CurrencySymbol(settings.CurrencyCode);
        var currentId = SelectedCategory?.Id;
        Categories.Clear();
        CategoryOptions.Clear();
        var categories = await finance.GetCategoriesAsync();
        var recentIds = (await finance.GetExpensesAsync()).Take(50).GroupBy(x => x.CategoryId).OrderByDescending(g => g.Max(x => x.OccurredOn)).Select(g => g.Key).ToList();
        foreach (var category in categories.OrderBy(x =>
        {
            var index = recentIds.IndexOf(x.Id);
            return index < 0 ? int.MaxValue : index;
        }).ThenBy(x => x.Name))
        {
            Categories.Add(category);
            CategoryOptions.Add(new ExpenseCategoryOption(category));
        }
        var preferredId = currentId ?? ParseCategoryId(settings.RememberLastCategory ? settings.LastCategoryId : settings.DefaultCategoryId) ?? ParseCategoryId(settings.DefaultCategoryId);
        SelectedCategory = Categories.FirstOrDefault(x => x.Id == preferredId) ?? Categories.FirstOrDefault();
        UpdateDateButtons();
        UpdateSaveButtonText();
    }

    [RelayCommand] private void SelectCategory(ExpenseCategoryOption option) => SelectedCategory = option.Category;

    public async Task ApplyReceiptAsync(string imagePath)
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = string.Empty;
        StatusMessage = "Lecture du ticket…";
        try
        {
            var result = await ocr.RecognizeAsync(imagePath);
            if (result is null || string.IsNullOrWhiteSpace(result.Text))
            {
                ErrorMessage = "Aucun texte n’a été reconnu sur le ticket.";
                return;
            }

            var draft = ReceiptParser.Parse(result.Text);
            if (draft.AmountMinor is long amount) Amount = (amount / 100m).ToString("0.00", CultureInfo.GetCultureInfo("fr-FR"));
            if (draft.OccurredOn is DateTime date) Date = date;
            if (!string.IsNullOrWhiteSpace(draft.MerchantName))
            {
                Description = draft.MerchantName;
            }
            StatusMessage = draft.AmountMinor is null ? "Ticket lu. Vérifiez les champs." : "Ticket lu. Vérifiez les champs avant d’enregistrer.";
        }
        catch (Exception ex) { ErrorMessage = $"Lecture du ticket impossible : {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void SelectDate(string shortcut)
    {
        IsCustomDateVisible = shortcut == "Autre";
        if (shortcut == "Aujourd’hui") Date = clock.GetLocalNow().Date;
        if (shortcut == "Hier") Date = clock.GetLocalNow().Date.AddDays(-1);
        UpdateDateButtons();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        ErrorMessage = string.Empty;
        if (!TryAmount(Amount, out var minor) || minor <= 0) { ErrorMessage = "Indiquez un montant valide."; return; }
        if (SelectedCategory is null) { ErrorMessage = "Choisissez une catégorie."; return; }
        IsBusy = true;
        try
        {
            if (_editingId is Guid editingId)
                await finance.UpdateExpenseAsync(editingId, minor, Description, Date, SelectedCategory.Id, settings.CurrencyCode);
            else
                await finance.AddExpenseAsync(minor, Description, Date, SelectedCategory.Id, settings.CurrencyCode);
            var keepOpen = _editingId is null && settings.AfterSaveBehavior == "Ajouter une autre";
            Amount = Description = string.Empty;
            Date = clock.GetLocalNow().Date;
            _editingId = null;
            if (keepOpen)
            {
                StatusMessage = "Dépense ajoutée. Vous pouvez saisir la suivante.";
                UpdateDateButtons();
            }
            else Saved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) { ErrorMessage = $"Enregistrement impossible : {ex.Message}"; }
        finally { IsBusy = false; }
    }

    internal static bool TryAmount(string value, out long minor)
    {
        if (decimal.TryParse(value.Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        { minor = checked((long)Math.Round(parsed * 100m, MidpointRounding.AwayFromZero)); return true; }
        minor = 0; return false;
    }

    public async Task BeginEditAsync(Expense expense)
    {
        await LoadAsync();
        _editingId = expense.Id;
        PageTitle = "Modifier la dépense";
        SaveButtonText = "Enregistrer les modifications";
        Amount = (expense.AmountMinor / 100m).ToString("0.00", CultureInfo.GetCultureInfo("fr-FR"));
        Description = expense.Description;
        Date = expense.OccurredOn;
        SelectedCategory = Categories.FirstOrDefault(x => x.Id == expense.CategoryId);
        IsCustomDateVisible = Date.Date != clock.GetLocalNow().Date && Date.Date != clock.GetLocalNow().Date.AddDays(-1);
        UpdateDateButtons();
    }

    private void UpdateSaveButtonText()
    {
        if (_editingId is not null) { SaveButtonText = "Enregistrer les modifications"; return; }
        SaveButtonText = TryAmount(Amount, out var minor) && minor > 0 ? $"Ajouter {DashboardViewModel.Money(minor, settings.CurrencyCode)}" : "Ajouter la dépense";
    }

    private void UpdateDateButtons()
    {
        var today = Date.Date == clock.GetLocalNow().Date;
        var yesterday = Date.Date == clock.GetLocalNow().Date.AddDays(-1);
        IsTodaySelected = today;
        IsYesterdaySelected = yesterday;
        IsOtherDateSelected = !today && !yesterday;
        OtherDateText = !today && !yesterday ? Date.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("fr-FR")) : "Autre";
    }

    private static Guid? ParseCategoryId(string value) => Guid.TryParse(value, out var id) ? id : null;
}

public partial class SubscriptionsViewModel(FinanceService finance, ISettingsService settings, IReminderService reminders, TimeProvider clock) : ObservableObject
{
    public ObservableCollection<SubscriptionRow> Subscriptions { get; } = [];
    public IReadOnlyList<string> Frequencies { get; } = ["Mensuel", "Trimestriel", "Annuel"];
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string amount = string.Empty;
    [ObservableProperty] private string currencySymbol = "€";
    [ObservableProperty] private string selectedFrequency = "Mensuel";
    [ObservableProperty] private DateTime nextBillingDate = clock.GetLocalNow().Date.AddMonths(1);
    [ObservableProperty] private string monthlyTotal = "0,00 € / mois";
    [ObservableProperty] private string annualTotal = "0,00 € / an";
    [ObservableProperty] private string errorMessage = string.Empty;
    [ObservableProperty] private bool isEmpty;
    [ObservableProperty] private bool isEditing;
    [ObservableProperty] private string formTitle = "Ajouter un abonnement";
    [ObservableProperty] private string saveButtonText = "Ajouter l’abonnement";
    private Subscription? _editingSubscription;

    [RelayCommand]
    public async Task LoadAsync()
    {
        CurrencySymbol = DashboardViewModel.CurrencySymbol(settings.CurrencyCode);
        var list = await finance.GetSubscriptionsAsync();
        Subscriptions.Clear();
        foreach (var item in list)
            Subscriptions.Add(new(item.Name, settings.HideAmounts ? "••••" : DashboardViewModel.Money(item.AmountMinor, settings.CurrencyCode), item.BillingIntervalMonths switch { 3 => "Tous les 3 mois", 12 => "Annuel", _ => "Mensuel" }, item.NextBillingDate.ToString("dd MMM yyyy", CultureInfo.GetCultureInfo("fr-FR")), item));
        var monthly = list.Sum(x => x.MonthlyEquivalentMinor);
        MonthlyTotal = settings.HideAmounts ? "•••• / mois" : $"{DashboardViewModel.Money(monthly, settings.CurrencyCode)} / mois";
        AnnualTotal = settings.HideAmounts ? "•••• / an" : $"{DashboardViewModel.Money(monthly * 12, settings.CurrencyCode)} / an";
        IsEmpty = list.Count == 0;
        if (settings.SubscriptionRemindersEnabled)
            await reminders.ShowSubscriptionReminderAsync(list, settings.SubscriptionReminderDays);
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        ErrorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(Name)) { ErrorMessage = "Indiquez le nom de l’abonnement."; return; }
        if (!AddExpenseViewModel.TryAmount(Amount, out var minor) || minor <= 0) { ErrorMessage = "Indiquez un montant valide."; return; }
        var interval = SelectedFrequency switch { "Trimestriel" => 3, "Annuel" => 12, _ => 1 };
        if (_editingSubscription is null)
            await finance.AddSubscriptionAsync(Name, minor, interval, NextBillingDate, settings.CurrencyCode);
        else
            await finance.UpdateSubscriptionAsync(_editingSubscription.Id, Name, minor, interval, NextBillingDate, settings.CurrencyCode);
        CancelEdit();
        await LoadAsync();
    }

    [RelayCommand]
    private void Edit(SubscriptionRow item)
    {
        _editingSubscription = item.Source;
        Name = item.Source.Name;
        Amount = (item.Source.AmountMinor / 100m).ToString("0.00", CultureInfo.GetCultureInfo("fr-FR"));
        SelectedFrequency = item.Source.BillingIntervalMonths switch { 3 => "Trimestriel", 12 => "Annuel", _ => "Mensuel" };
        NextBillingDate = item.Source.NextBillingDate;
        IsEditing = true;
        FormTitle = "Modifier l’abonnement";
        SaveButtonText = "Enregistrer les modifications";
        ErrorMessage = string.Empty;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        _editingSubscription = null;
        Name = Amount = string.Empty;
        SelectedFrequency = "Mensuel";
        NextBillingDate = clock.GetLocalNow().Date.AddMonths(1);
        IsEditing = false;
        FormTitle = "Ajouter un abonnement";
        SaveButtonText = "Ajouter l’abonnement";
        ErrorMessage = string.Empty;
    }

    [RelayCommand]
    private async Task DeleteAsync(SubscriptionRow item)
    {
        if (_editingSubscription?.Id == item.Source.Id) CancelEdit();
        await finance.DeleteSubscriptionAsync(item.Source.Id);
        await LoadAsync();
    }
}

public partial class SettingsViewModel(
    FinanceService finance,
    ISettingsService settings,
    IExportService export,
    IDataBackupService backup,
    IBiometricService biometrics,
    IAppLockService appLock) : ObservableObject
{
    private bool _loading;
    private bool _updatingColor;
    private Category? _editingCategory;
    public ObservableCollection<Category> Categories { get; } = [];
    public IReadOnlyList<string> Themes { get; } = ["Système", "Clair", "Sombre"];
    public IReadOnlyList<int> AutoLockChoices { get; } = [1, 2, 5, 10, 30];
    public IReadOnlyList<int> ReminderDayChoices { get; } = [1, 3, 7];
    public IReadOnlyList<string> Currencies { get; } = ["EUR", "USD", "CHF", "GBP", "CAD", "JPY"];
    public IReadOnlyList<string> AfterSaveChoices { get; } = ["Fermer", "Ajouter une autre"];
    public IReadOnlyList<string> CategoryColors { get; } = ["#6750A4", "#2E7D32", "#1565C0", "#C62828", "#EF6C00", "#546E7A", "#7D5260", "#00838F"];

    [ObservableProperty] private string newCategoryName = string.Empty;
    [ObservableProperty] private string categoryBudget = string.Empty;
    [ObservableProperty] private string categorySaveText = "Ajouter la catégorie";
    [ObservableProperty] private bool isEditingCategory;
    [ObservableProperty] private string selectedColor = "#6750A4";
    [ObservableProperty] private string colorHexInput = "#6750A4";
    [ObservableProperty] private double red = 103;
    [ObservableProperty] private double green = 80;
    [ObservableProperty] private double blue = 164;
    [ObservableProperty] private bool hideAmounts;
    [ObservableProperty] private bool biometricsEnabled;
    [ObservableProperty] private bool biometricsAvailable;
    [ObservableProperty] private int autoLockMinutes;
    [ObservableProperty] private string selectedTheme = "Système";
    [ObservableProperty] private bool autoCreateSubscriptionExpenses;
    [ObservableProperty] private bool autoCreateRecurringIncomes;
    [ObservableProperty] private bool subscriptionRemindersEnabled;
    [ObservableProperty] private int subscriptionReminderDays = 3;
    [ObservableProperty] private string monthlyBudget = string.Empty;
    [ObservableProperty] private string selectedCurrency = "EUR";
    [ObservableProperty] private Category? defaultCategory;
    [ObservableProperty] private bool rememberLastCategory = true;
    [ObservableProperty] private string selectedAfterSaveBehavior = "Fermer";
    [ObservableProperty] private string currentPin = string.Empty;
    [ObservableProperty] private string newPin = string.Empty;
    [ObservableProperty] private string confirmPin = string.Empty;
    [ObservableProperty] private string backupPassword = string.Empty;
    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private bool isBusy;

    [RelayCommand]
    public async Task LoadAsync()
    {
        _loading = true;
        try
        {
            HideAmounts = settings.HideAmounts;
            BiometricsEnabled = settings.BiometricsEnabled;
            BiometricsAvailable = biometrics.IsAvailable;
            AutoLockMinutes = settings.AutoLockMinutes;
            SelectedTheme = settings.Theme;
            AutoCreateSubscriptionExpenses = settings.AutoCreateSubscriptionExpenses;
            AutoCreateRecurringIncomes = settings.AutoCreateRecurringIncomes;
            SubscriptionRemindersEnabled = settings.SubscriptionRemindersEnabled;
            SubscriptionReminderDays = settings.SubscriptionReminderDays;
            MonthlyBudget = settings.MonthlyBudgetMinor > 0 ? (settings.MonthlyBudgetMinor / 100m).ToString("0.##", CultureInfo.GetCultureInfo("fr-FR")) : string.Empty;
            SelectedCurrency = settings.CurrencyCode;
            RememberLastCategory = settings.RememberLastCategory;
            SelectedAfterSaveBehavior = settings.AfterSaveBehavior;
            await ReloadCategoriesAsync();
            DefaultCategory = Guid.TryParse(settings.DefaultCategoryId, out var id) ? Categories.FirstOrDefault(x => x.Id == id) : null;
        }
        finally { _loading = false; }
    }

    partial void OnHideAmountsChanged(bool value) { if (!_loading) settings.HideAmounts = value; }
    partial void OnBiometricsEnabledChanged(bool value) { if (!_loading && BiometricsAvailable) settings.BiometricsEnabled = value; }
    partial void OnAutoLockMinutesChanged(int value) { if (!_loading && value > 0) settings.AutoLockMinutes = value; }
    partial void OnSelectedThemeChanged(string value) { if (!_loading && !string.IsNullOrWhiteSpace(value)) settings.Theme = value; }
    partial void OnAutoCreateSubscriptionExpensesChanged(bool value) { if (!_loading) settings.AutoCreateSubscriptionExpenses = value; }
    partial void OnAutoCreateRecurringIncomesChanged(bool value) { if (!_loading) settings.AutoCreateRecurringIncomes = value; }
    partial void OnSubscriptionRemindersEnabledChanged(bool value) { if (!_loading) settings.SubscriptionRemindersEnabled = value; }
    partial void OnSubscriptionReminderDaysChanged(int value) { if (!_loading && value > 0) settings.SubscriptionReminderDays = value; }
    partial void OnSelectedCurrencyChanged(string value) { if (!_loading && !string.IsNullOrWhiteSpace(value)) settings.CurrencyCode = value; }
    partial void OnDefaultCategoryChanged(Category? value) { if (!_loading) settings.DefaultCategoryId = value?.Id.ToString() ?? string.Empty; }
    partial void OnRememberLastCategoryChanged(bool value) { if (!_loading) settings.RememberLastCategory = value; }
    partial void OnSelectedAfterSaveBehaviorChanged(string value) { if (!_loading && !string.IsNullOrWhiteSpace(value)) settings.AfterSaveBehavior = value; }
    partial void OnRedChanged(double value) => UpdateColorFromRgb();
    partial void OnGreenChanged(double value) => UpdateColorFromRgb();
    partial void OnBlueChanged(double value) => UpdateColorFromRgb();
    partial void OnColorHexInputChanged(string value)
    {
        if (_updatingColor || !TryParseColor(value, out var r, out var g, out var b)) return;
        SetColor(r, g, b);
    }

    [RelayCommand]
    private void SelectSuggestedColor(string color)
    {
        if (TryParseColor(color, out var r, out var g, out var b)) SetColor(r, g, b);
    }

    [RelayCommand]
    private async Task SaveCategoryAsync()
    {
        StatusMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(NewCategoryName)) { StatusMessage = "Indiquez un nom de catégorie."; return; }
        long? budgetMinor = null;
        if (!string.IsNullOrWhiteSpace(CategoryBudget))
        {
            if (!AddExpenseViewModel.TryAmount(CategoryBudget, out var parsed) || parsed <= 0) { StatusMessage = "Le budget de catégorie n’est pas valide."; return; }
            budgetMinor = parsed;
        }
        if (_editingCategory is null)
        {
            await finance.AddCategoryAsync(NewCategoryName, SelectedColor, budgetMinor);
            StatusMessage = "Catégorie ajoutée.";
        }
        else
        {
            await finance.UpdateCategoryAsync(_editingCategory.Id, NewCategoryName, SelectedColor, budgetMinor);
            StatusMessage = "Catégorie mise à jour.";
        }
        CancelCategoryEdit();
        await ReloadCategoriesAsync();
    }

    [RelayCommand]
    private void EditCategory(Category category)
    {
        _editingCategory = category;
        NewCategoryName = category.Name;
        CategoryBudget = category.BudgetMinor is long budget ? (budget / 100m).ToString("0.##", CultureInfo.GetCultureInfo("fr-FR")) : string.Empty;
        if (TryParseColor(category.Color, out var r, out var g, out var b)) SetColor(r, g, b);
        IsEditingCategory = true;
        CategorySaveText = "Enregistrer les modifications";
    }

    [RelayCommand]
    private void CancelCategoryEdit()
    {
        _editingCategory = null;
        NewCategoryName = CategoryBudget = string.Empty;
        IsEditingCategory = false;
        CategorySaveText = "Ajouter la catégorie";
    }

    [RelayCommand]
    private async Task DeleteCategoryAsync(Category category)
    {
        try { await finance.DeleteCategoryAsync(category.Id); StatusMessage = "Catégorie supprimée."; await ReloadCategoriesAsync(); }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    [RelayCommand]
    private void SaveBudget()
    {
        if (string.IsNullOrWhiteSpace(MonthlyBudget)) { settings.MonthlyBudgetMinor = 0; StatusMessage = "Budget mensuel désactivé."; return; }
        if (!AddExpenseViewModel.TryAmount(MonthlyBudget, out var minor) || minor <= 0) { StatusMessage = "Indiquez un budget mensuel valide."; return; }
        settings.MonthlyBudgetMinor = minor;
        StatusMessage = "Budget mensuel enregistré.";
    }

    [RelayCommand]
    private async Task ChangePinAsync()
    {
        if (!await appLock.VerifyPinAsync(CurrentPin)) { StatusMessage = "Le code PIN actuel est incorrect."; return; }
        if (NewPin.Length is < 4 or > 8 || NewPin.Any(x => !char.IsDigit(x))) { StatusMessage = "Le nouveau PIN doit contenir entre 4 et 8 chiffres."; return; }
        if (NewPin != ConfirmPin) { StatusMessage = "La confirmation du nouveau PIN ne correspond pas."; return; }
        await appLock.SetPinAsync(NewPin);
        CurrentPin = NewPin = ConfirmPin = string.Empty;
        StatusMessage = "Code PIN modifié.";
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        await export.ExportExpensesCsvAsync();
        StatusMessage = "Export CSV prêt à être partagé.";
    }

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        try { IsBusy = true; await backup.ExportEncryptedAsync(BackupPassword); StatusMessage = "Sauvegarde chiffrée prête à être partagée."; }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    public async Task RestoreBackupAsync()
    {
        try
        {
            IsBusy = true;
            if (await backup.ImportEncryptedAsync(BackupPassword)) { StatusMessage = "Sauvegarde restaurée."; await ReloadCategoriesAsync(); }
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    public async Task DeleteAllDataAsync(string pin)
    {
        try
        {
            if (!await appLock.VerifyPinAsync(pin)) { StatusMessage = "Code PIN incorrect. Suppression annulée."; return; }
            IsBusy = true; await finance.DeleteAllDataAsync(); StatusMessage = "Toutes les données financières ont été supprimées."; await ReloadCategoriesAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task ReloadCategoriesAsync()
    {
        Categories.Clear();
        foreach (var category in await finance.GetCategoriesAsync()) Categories.Add(category);
    }

    private void UpdateColorFromRgb()
    {
        if (_updatingColor) return;
        _updatingColor = true;
        SelectedColor = $"#{(int)Math.Round(Red):X2}{(int)Math.Round(Green):X2}{(int)Math.Round(Blue):X2}";
        ColorHexInput = SelectedColor;
        _updatingColor = false;
    }

    private void SetColor(byte r, byte g, byte b)
    {
        _updatingColor = true;
        Red = r; Green = g; Blue = b;
        SelectedColor = $"#{r:X2}{g:X2}{b:X2}";
        ColorHexInput = SelectedColor;
        _updatingColor = false;
    }

    private static bool TryParseColor(string value, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;
        var hex = value.Trim().TrimStart('#');
        return hex.Length == 6 && byte.TryParse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r)
            && byte.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g)
            && byte.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b);
    }
}
