using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Android17.Application;

namespace Finance.Android17.Presentation;

public partial class AddExpenseViewModel(FinanceService finance, ISettingsService settings, IOcrService ocr, TimeProvider clock) : ObservableObject
{
    private const string NewExpenseTitle = "Nouvelle dépense";
    private const string SaveEditsText = "Enregistrer les modifications";

    public ObservableCollection<ExpenseCategoryOption> CategoryOptions { get; } = [];
    public event EventHandler? Saved;
    [ObservableProperty] private string amount = string.Empty;
    [ObservableProperty] private string currencySymbol = "€";
    [ObservableProperty] private string description = string.Empty;
    [ObservableProperty] private DateTime date = clock.GetLocalNow().Date;
    [ObservableProperty] private ExpenseCategoryOption? selectedCategory;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isSaveEnabled = true;
    [ObservableProperty] private bool isCustomDateVisible;
    [ObservableProperty] private string errorMessage = string.Empty;
    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private string pageTitle = NewExpenseTitle;
    [ObservableProperty] private string saveButtonText = "Ajouter la dépense";
    [ObservableProperty] private bool isTodaySelected = true;
    [ObservableProperty] private bool isYesterdaySelected;
    [ObservableProperty] private bool isOtherDateSelected;
    [ObservableProperty] private string otherDateText = "Autre";
    private Guid? _editingId;

    partial void OnIsBusyChanged(bool value) => IsSaveEnabled = !value;
    partial void OnAmountChanged(string value) => UpdateSaveButtonText();
    partial void OnDateChanged(DateTime value) => UpdateDateButtons();
    partial void OnSelectedCategoryChanged(ExpenseCategoryOption? value)
    {
        if (value is not null && settings.RememberLastCategory) settings.LastCategoryId = value.Id.ToString();
        foreach (var option in CategoryOptions) option.IsSelected = option.Id == value?.Id;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        CurrencySymbol = MoneyFormatter.CurrencySymbol(settings.CurrencyCode);
        var currentId = SelectedCategory?.Id;
        CategoryOptions.Clear();
        var categories = CategoryChoice.OrderByRecentUse(await finance.GetCategoriesAsync(), await finance.GetExpensesAsync());
        foreach (var category in categories) CategoryOptions.Add(new ExpenseCategoryOption(category.Id, category.Name, category.Color));
        var preferred = CategoryChoice.Preferred(categories, currentId, settings.LastCategoryId, settings.DefaultCategoryId, settings.RememberLastCategory);
        SelectedCategory = CategoryOptions.FirstOrDefault(x => x.Id == preferred?.Id);
        UpdateDateButtons();
        UpdateSaveButtonText();
    }

    [RelayCommand] private void SelectCategory(ExpenseCategoryOption option) => SelectedCategory = option;

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
            if (draft.AmountMinor is long amountMinor) Amount = MoneyFormatter.FixedAmount(amountMinor);
            if (draft.OccurredOn is DateTime occurredOn) Date = occurredOn;
            if (!string.IsNullOrWhiteSpace(draft.MerchantName)) Description = draft.MerchantName;
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
        if (!AmountParser.TryParse(Amount, out var minor) || minor <= 0) { ErrorMessage = "Indiquez un montant valide."; return; }
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

    public async Task BeginEditAsync(Guid expenseId)
    {
        var expense = (await finance.GetExpensesAsync()).FirstOrDefault(x => x.Id == expenseId);
        if (expense is null) return;
        await LoadAsync();
        _editingId = expense.Id;
        PageTitle = "Modifier la dépense";
        SaveButtonText = SaveEditsText;
        Amount = MoneyFormatter.FixedAmount(expense.AmountMinor);
        Description = expense.Description;
        Date = expense.OccurredOn;
        SelectedCategory = CategoryOptions.FirstOrDefault(x => x.Id == expense.CategoryId);
        IsCustomDateVisible = Date.Date != clock.GetLocalNow().Date && Date.Date != clock.GetLocalNow().Date.AddDays(-1);
        UpdateDateButtons();
    }

    private void UpdateSaveButtonText()
    {
        if (_editingId is not null) { SaveButtonText = SaveEditsText; return; }
        SaveButtonText = AmountParser.TryParse(Amount, out var minor) && minor > 0 ? $"Ajouter {MoneyFormatter.Money(minor, settings.CurrencyCode)}" : "Ajouter la dépense";
    }

    private void UpdateDateButtons()
    {
        var today = Date.Date == clock.GetLocalNow().Date;
        var yesterday = Date.Date == clock.GetLocalNow().Date.AddDays(-1);
        IsTodaySelected = today;
        IsYesterdaySelected = yesterday;
        IsOtherDateSelected = !today && !yesterday;
        OtherDateText = !today && !yesterday ? Date.ToString("dd/MM/yyyy", MoneyFormatter.French) : "Autre";
    }
}
