using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Android17.Application;
using Finance.Android17.Domain;

namespace Finance.Android17.Presentation;

/// <summary>Page « Réglages » : préférences, verrouillage, code PIN, budget, devise et automatismes. Les catégories et les données ont leur propre ViewModel.</summary>
public partial class SettingsViewModel(
    ICategoryRepository categoryRepository,
    ISettingsService settings,
    IBiometricService biometrics,
    IAppLockService appLock,
    ISubscriptionRepository subscriptionRepository,
    SubscriptionReminderScheduler reminders,
    IDailyCheckScheduler dailyCheck,
    IFinanceDataStore dataStore) : StatusViewModelBase
{
    private bool _loading;
    public ObservableCollection<CategoryItem> Categories { get; } = [];
    public IReadOnlyList<string> Themes { get; } = ThemeOptions.All;

    /// <summary>Les thèmes dans l'ordre du sélecteur à segments : clair, sombre, puis celui de l'appareil.</summary>
    public IReadOnlyList<string> ThemeChoices { get; } = [ThemeOptions.Light, ThemeOptions.Dark, ThemeOptions.FollowSystem];
    public IReadOnlyList<int> AutoLockChoices { get; } = [1, 2, 5, 10, 30];
    public IReadOnlyList<int> ReminderDayChoices { get; } = [1, 3, 7];
    public IReadOnlyList<string> Currencies { get; } = CurrencyCodes.Supported;
    public IReadOnlyList<string> AfterSaveChoices { get; } = AfterSaveOptions.All;

    [ObservableProperty] private bool hideAmounts;
    [ObservableProperty] private bool biometricsEnabled;
    [ObservableProperty] private bool biometricsAvailable;
    [ObservableProperty] private int autoLockMinutes;
    [ObservableProperty] private string selectedTheme = ThemeOptions.FollowSystem;
    [ObservableProperty] private bool autoCreateSubscriptionExpenses;
    [ObservableProperty] private bool autoCreateRecurringIncomes;
    [ObservableProperty] private bool subscriptionRemindersEnabled;
    [ObservableProperty] private int subscriptionReminderDays = 3;
    [ObservableProperty] private bool backgroundAlertsEnabled;
    [ObservableProperty] private string monthlyBudget = string.Empty;
    [ObservableProperty] private string selectedCurrency = CurrencyCodes.Default;
    [ObservableProperty] private CategoryItem? defaultCategory;
    [ObservableProperty] private bool rememberLastCategory = true;
    [ObservableProperty] private string selectedAfterSaveBehavior = AfterSaveOptions.Close;
    [ObservableProperty] private string currentPin = string.Empty;
    [ObservableProperty] private string newPin = string.Empty;
    [ObservableProperty] private string confirmPin = string.Empty;

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
            BackgroundAlertsEnabled = settings.BackgroundAlertsEnabled;
            MonthlyBudget = settings.MonthlyBudgetMinor > 0 ? MoneyFormatter.PlainAmount(settings.MonthlyBudgetMinor) : string.Empty;
            SelectedCurrency = settings.CurrencyCode;
            RememberLastCategory = settings.RememberLastCategory;
            SelectedAfterSaveBehavior = settings.AfterSaveBehavior;
            Categories.Clear();
            foreach (var category in await categoryRepository.GetAllAsync()) Categories.Add(CategoryItem.From(category));
            DefaultCategory = Guid.TryParse(settings.DefaultCategoryId, out var id) ? Categories.FirstOrDefault(x => x.Id == id) : null;
        }
        finally { _loading = false; }
    }

    partial void OnHideAmountsChanged(bool value) { if (!_loading) settings.HideAmounts = value; }
    partial void OnBiometricsEnabledChanged(bool value) { if (!_loading && BiometricsAvailable) settings.BiometricsEnabled = value; }
    partial void OnAutoLockMinutesChanged(int value) { if (!_loading && value > 0) settings.AutoLockMinutes = value; }
    partial void OnSelectedThemeChanged(string value) { if (!_loading && !string.IsNullOrWhiteSpace(value)) settings.Theme = value; }

    /// <summary>Choix direct d'un thème (sélecteur à trois segments) ; <paramref name="theme"/> est une valeur de <see cref="ThemeOptions"/>.</summary>
    [RelayCommand]
    private void SelectTheme(string theme)
    {
        if (ThemeOptions.All.Contains(theme)) SelectedTheme = theme;
    }

    partial void OnAutoCreateSubscriptionExpensesChanged(bool value) { if (!_loading) settings.AutoCreateSubscriptionExpenses = value; }
    partial void OnAutoCreateRecurringIncomesChanged(bool value) { if (!_loading) settings.AutoCreateRecurringIncomes = value; }
    partial void OnSubscriptionRemindersEnabledChanged(bool value)
    {
        if (_loading) return;
        settings.SubscriptionRemindersEnabled = value;
        _ = RefreshRemindersAsync();
    }

    partial void OnSubscriptionReminderDaysChanged(int value)
    {
        if (_loading || value <= 0) return;
        settings.SubscriptionReminderDays = value;
        _ = RefreshRemindersAsync();
    }

    partial void OnBackgroundAlertsEnabledChanged(bool value)
    {
        if (_loading) return;
        settings.BackgroundAlertsEnabled = value;
        // L'autorisation de notifier se demande ici, au premier plan, plutôt que pendant le contrôle de 8 h.
        if (value) _ = dailyCheck.EnsureScheduledAsync();
    }

    partial void OnSelectedCurrencyChanged(string value) { if (!_loading && !string.IsNullOrWhiteSpace(value)) _ = ChangeCurrencyAsync(value); }

    /// <summary>Les totaux additionnent les montants sans conversion : changer de devise une fois des données saisies les mélangerait. Le changement n'est donc possible que sur une base vide.</summary>
    private async Task ChangeCurrencyAsync(string currency)
    {
        if (currency == settings.CurrencyCode) return;
        try
        {
            var snapshot = await dataStore.CreateSnapshotAsync();
            var hasData = snapshot.Expenses.Count > 0 || snapshot.Subscriptions.Count > 0 || snapshot.Incomes?.Count > 0 || snapshot.RecurringIncomes?.Count > 0;
            if (!hasData) { settings.CurrencyCode = currency; return; }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ShowError(Describe(ex, "Changement de devise impossible."));
        }
        _loading = true;
        try { SelectedCurrency = settings.CurrencyCode; }
        finally { _loading = false; }
        ShowError("La devise ne peut plus changer une fois des données saisies : leurs montants seraient mélangés. Supprimez d'abord les données (Réglages > Données).");
    }
    partial void OnDefaultCategoryChanged(CategoryItem? value) { if (!_loading) settings.DefaultCategoryId = value?.Id.ToString() ?? string.Empty; }
    partial void OnRememberLastCategoryChanged(bool value) { if (!_loading) settings.RememberLastCategory = value; }
    partial void OnSelectedAfterSaveBehaviorChanged(string value) { if (!_loading && !string.IsNullOrWhiteSpace(value)) settings.AfterSaveBehavior = value; }

    [RelayCommand]
    private void SaveBudget()
    {
        if (string.IsNullOrWhiteSpace(MonthlyBudget))
        {
            settings.MonthlyBudgetMinor = 0;
            StatusMessage = "Budget mensuel désactivé.";
            return;
        }
        if (!AmountParser.TryParse(MonthlyBudget, out var minor) || minor <= 0)
        {
            ShowError("Indiquez un budget mensuel valide.");
            return;
        }
        settings.MonthlyBudgetMinor = minor;
        StatusMessage = "Budget mensuel enregistré.";
    }

    [RelayCommand]
    private async Task ChangePinAsync()
    {
        if (!await appLock.VerifyPinAsync(CurrentPin))
        {
            ShowError(await PinLockMessages.AfterFailureAsync(appLock, "Le code PIN actuel est incorrect."));
            return;
        }
        if (!PinPolicy.IsValid(NewPin))
        {
            ShowError($"Le nouveau PIN doit contenir {PinPolicy.RequirementText}.");
            return;
        }
        if (NewPin != ConfirmPin)
        {
            ShowError("La confirmation du nouveau PIN ne correspond pas.");
            return;
        }
        await appLock.SetPinAsync(NewPin);
        CurrentPin = NewPin = ConfirmPin = string.Empty;
        StatusMessage = "Code PIN modifié.";
    }

    // Les rappels dépendent des abonnements et des réglages : ils sont replanifiés dès qu'un réglage change.
    private async Task RefreshRemindersAsync()
    {
        try { await reminders.RefreshAsync(await subscriptionRepository.GetAllAsync()); }
        catch (Exception ex) { StatusMessage = $"{StatusMessage} Rappels non mis à jour : {Describe(ex, "erreur inattendue.")}"; }
    }
}
