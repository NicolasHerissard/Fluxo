using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Android17.Application;

namespace Finance.Android17.Presentation;

public partial class SettingsViewModel(
    ICategoryRepository categoryRepository,
    IFinanceDataStore dataStore,
    ISettingsService settings,
    IExportService export,
    IDataBackupService backup,
    IBiometricService biometrics,
    IAppLockService appLock,
    ISubscriptionRepository subscriptionRepository,
    SubscriptionReminderScheduler reminders) : ObservableObject
{
    private bool _loading;
    private bool _updatingColor;
    private Guid? _editingCategoryId;
    public ObservableCollection<CategoryItem> Categories { get; } = [];
    public IReadOnlyList<string> Themes { get; } = ThemeOptions.All;
    public IReadOnlyList<int> AutoLockChoices { get; } = [1, 2, 5, 10, 30];
    public IReadOnlyList<int> ReminderDayChoices { get; } = [1, 3, 7];
    public IReadOnlyList<string> Currencies { get; } = ["EUR", "USD", "CHF", "GBP", "CAD", "JPY"];
    public IReadOnlyList<string> AfterSaveChoices { get; } = AfterSaveOptions.All;
    public IReadOnlyList<string> CategoryColors { get; } = [Palette.Primary, "#2E7D32", "#1565C0", "#C62828", "#EF6C00", "#546E7A", "#7D5260", "#00838F"];

    [ObservableProperty] private string newCategoryName = string.Empty;
    [ObservableProperty] private string categoryBudget = string.Empty;
    [ObservableProperty] private string categorySaveText = "Ajouter la catégorie";
    [ObservableProperty] private bool isEditingCategory;
    [ObservableProperty] private string selectedColor = Palette.Primary;
    [ObservableProperty] private string colorHexInput = Palette.Primary;
    [ObservableProperty] private double red = 103;
    [ObservableProperty] private double green = 80;
    [ObservableProperty] private double blue = 164;
    [ObservableProperty] private bool hideAmounts;
    [ObservableProperty] private bool biometricsEnabled;
    [ObservableProperty] private bool biometricsAvailable;
    [ObservableProperty] private int autoLockMinutes;
    [ObservableProperty] private string selectedTheme = ThemeOptions.FollowSystem;
    [ObservableProperty] private bool autoCreateSubscriptionExpenses;
    [ObservableProperty] private bool autoCreateRecurringIncomes;
    [ObservableProperty] private bool subscriptionRemindersEnabled;
    [ObservableProperty] private int subscriptionReminderDays = 3;
    [ObservableProperty] private string monthlyBudget = string.Empty;
    [ObservableProperty] private string selectedCurrency = "EUR";
    [ObservableProperty] private CategoryItem? defaultCategory;
    [ObservableProperty] private bool rememberLastCategory = true;
    [ObservableProperty] private string selectedAfterSaveBehavior = AfterSaveOptions.Close;
    [ObservableProperty] private string currentPin = string.Empty;
    [ObservableProperty] private string newPin = string.Empty;
    [ObservableProperty] private string confirmPin = string.Empty;
    [ObservableProperty] private string backupPassword = string.Empty;
    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private bool statusIsError;

    partial void OnStatusMessageChanged(string value) => StatusIsError = false;

    private void ShowError(string message)
    {
        StatusMessage = message;
        StatusIsError = true;
    }
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
            MonthlyBudget = settings.MonthlyBudgetMinor > 0 ? MoneyFormatter.PlainAmount(settings.MonthlyBudgetMinor) : string.Empty;
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
    partial void OnSelectedCurrencyChanged(string value) { if (!_loading && !string.IsNullOrWhiteSpace(value)) settings.CurrencyCode = value; }
    partial void OnDefaultCategoryChanged(CategoryItem? value) { if (!_loading) settings.DefaultCategoryId = value?.Id.ToString() ?? string.Empty; }
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
        if (string.IsNullOrWhiteSpace(NewCategoryName)) { ShowError("Indiquez un nom de catégorie."); return; }
        long? budgetMinor = null;
        if (!string.IsNullOrWhiteSpace(CategoryBudget))
        {
            if (!AmountParser.TryParse(CategoryBudget, out var parsed) || parsed <= 0) { ShowError("Le budget de catégorie n’est pas valide."); return; }
            budgetMinor = parsed;
        }
        if (_editingCategoryId is not Guid id)
        {
            await categoryRepository.AddAsync(NewCategoryName, SelectedColor, budgetMinor);
            StatusMessage = "Catégorie ajoutée.";
        }
        else
        {
            await categoryRepository.UpdateAsync(id, NewCategoryName, SelectedColor, budgetMinor);
            StatusMessage = "Catégorie mise à jour.";
        }
        CancelCategoryEdit();
        await ReloadCategoriesAsync();
    }

    [RelayCommand]
    private void EditCategory(CategoryItem category)
    {
        _editingCategoryId = category.Id;
        NewCategoryName = category.Name;
        CategoryBudget = category.BudgetMinor is long budget ? MoneyFormatter.PlainAmount(budget) : string.Empty;
        if (TryParseColor(category.Color, out var r, out var g, out var b)) SetColor(r, g, b);
        IsEditingCategory = true;
        CategorySaveText = "Enregistrer les modifications";
    }

    [RelayCommand]
    private void CancelCategoryEdit()
    {
        _editingCategoryId = null;
        NewCategoryName = CategoryBudget = string.Empty;
        IsEditingCategory = false;
        CategorySaveText = "Ajouter la catégorie";
    }

    [RelayCommand]
    private async Task DeleteCategoryAsync(CategoryItem category)
    {
        try { await categoryRepository.DeleteAsync(category.Id); StatusMessage = "Catégorie supprimée."; await ReloadCategoriesAsync(); }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    [RelayCommand]
    private void SaveBudget()
    {
        if (string.IsNullOrWhiteSpace(MonthlyBudget)) { settings.MonthlyBudgetMinor = 0; StatusMessage = "Budget mensuel désactivé."; return; }
        if (!AmountParser.TryParse(MonthlyBudget, out var minor) || minor <= 0) { ShowError("Indiquez un budget mensuel valide."); return; }
        settings.MonthlyBudgetMinor = minor;
        StatusMessage = "Budget mensuel enregistré.";
    }

    [RelayCommand]
    private async Task ChangePinAsync()
    {
        if (!await appLock.VerifyPinAsync(CurrentPin)) { ShowError(await PinLockMessages.AfterFailureAsync(appLock, "Le code PIN actuel est incorrect.")); return; }
        if (!PinPolicy.IsValid(NewPin)) { ShowError($"Le nouveau PIN doit contenir {PinPolicy.RequirementText}."); return; }
        if (NewPin != ConfirmPin) { ShowError("La confirmation du nouveau PIN ne correspond pas."); return; }
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
        catch (Exception ex) { ShowError(ex.Message); }
        finally { IsBusy = false; }
    }

    public async Task RestoreBackupAsync()
    {
        try
        {
            IsBusy = true;
            if (await backup.ImportEncryptedAsync(BackupPassword)) { StatusMessage = "Sauvegarde restaurée."; await ReloadCategoriesAsync(); await RefreshRemindersAsync(); }
        }
        catch (Exception ex) { ShowError(ex.Message); }
        finally { IsBusy = false; }
    }

    public async Task DeleteAllDataAsync(string pin)
    {
        try
        {
            if (!await appLock.VerifyPinAsync(pin)) { ShowError(await PinLockMessages.AfterFailureAsync(appLock, "Code PIN incorrect. Suppression annulée.")); return; }
            IsBusy = true; await dataStore.DeleteAllAsync(); StatusMessage = "Toutes les données financières ont été supprimées."; await ReloadCategoriesAsync();
            await RefreshRemindersAsync();
        }
        catch (Exception ex) { ShowError(ex.Message); }
        finally { IsBusy = false; }
    }

    // Les données ont été remplacées ou effacées : les alarmes de rappel doivent suivre.
    private async Task RefreshRemindersAsync()
    {
        try { await reminders.RefreshAsync(await subscriptionRepository.GetAllAsync()); }
        catch (Exception ex) { StatusMessage = $"{StatusMessage} Rappels non mis à jour : {ex.Message}"; }
    }

    private async Task ReloadCategoriesAsync()
    {
        Categories.Clear();
        foreach (var category in await categoryRepository.GetAllAsync()) Categories.Add(CategoryItem.From(category));
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
