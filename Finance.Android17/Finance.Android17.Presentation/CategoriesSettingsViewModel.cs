using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Android17.Application;

namespace Finance.Android17.Presentation;

/// <summary>Page « Gérer les catégories » : création, modification (nom, couleur, budget) et suppression.</summary>
public partial class CategoriesSettingsViewModel(ICategoryRepository categoryRepository) : StatusViewModelBase
{
    private const string AddText = "Ajouter la catégorie";
    private static readonly (byte R, byte G, byte B) PrimaryRgb = TryParseColor(Palette.Primary, out var r, out var g, out var b) ? (r, g, b) : default;

    private bool _updatingColor;
    private Guid? _editingCategoryId;

    public ObservableCollection<CategoryItem> Categories { get; } = [];
    public IReadOnlyList<string> CategoryColors { get; } = [Palette.Primary, "#2E7D32", "#1565C0", "#C62828", "#EF6C00", "#546E7A", "#7D5260", "#00838F"];

    [ObservableProperty] private string newCategoryName = string.Empty;
    [ObservableProperty] private string categoryBudget = string.Empty;
    [ObservableProperty] private string categorySaveText = AddText;
    [ObservableProperty] private bool isEditingCategory;
    [ObservableProperty] private string selectedColor = Palette.Primary;
    [ObservableProperty] private string colorHexInput = Palette.Primary;
    [ObservableProperty] private double red = PrimaryRgb.R;
    [ObservableProperty] private double green = PrimaryRgb.G;
    [ObservableProperty] private double blue = PrimaryRgb.B;

    [RelayCommand]
    public Task LoadAsync() => ReloadCategoriesAsync();

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
        if (string.IsNullOrWhiteSpace(NewCategoryName))
        {
            ShowError("Indiquez un nom de catégorie.");
            return;
        }
        long? budgetMinor = null;
        if (!string.IsNullOrWhiteSpace(CategoryBudget))
        {
            if (!AmountParser.TryParse(CategoryBudget, out var parsed) || parsed <= 0)
            {
                ShowError("Le budget de catégorie n’est pas valide.");
                return;
            }
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
        CategorySaveText = AddText;
    }

    [RelayCommand]
    private async Task DeleteCategoryAsync(CategoryItem category)
    {
        try
        {
            await categoryRepository.DeleteAsync(category.Id);
            StatusMessage = "Catégorie supprimée.";
            await ReloadCategoriesAsync();
        }
        catch (Exception ex)
        {
            ShowError(Describe(ex, "Suppression impossible."));
        }
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
        try
        {
            SelectedColor = $"#{(int)Math.Round(Red):X2}{(int)Math.Round(Green):X2}{(int)Math.Round(Blue):X2}";
            ColorHexInput = SelectedColor;
        }
        finally { _updatingColor = false; }
    }

    private void SetColor(byte r, byte g, byte b)
    {
        _updatingColor = true;
        try
        {
            Red = r;
            Green = g;
            Blue = b;
            SelectedColor = $"#{r:X2}{g:X2}{b:X2}";
            ColorHexInput = SelectedColor;
        }
        finally { _updatingColor = false; }
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
