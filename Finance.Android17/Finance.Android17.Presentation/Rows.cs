using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Finance.Android17.Application;
using Finance.Android17.Domain;

namespace Finance.Android17.Presentation;

// Modèles d'affichage : des textes prêts à lier et l'identifiant de l'élément, jamais l'entité métier.

public sealed record ExpenseRow(Guid Id, string Description, string Category, string Date, string Amount, string CategoryColor, string CategoryInitial)
{
    internal static ExpenseRow From(Expense expense, bool hideAmounts)
    {
        var category = expense.Category?.Name ?? Uncategorized.Name;
        return new(expense.Id, string.IsNullOrWhiteSpace(expense.Description) ? "Dépense" : expense.Description, category,
            MoneyFormatter.Date(expense.OccurredOn), hideAmounts ? MoneyFormatter.Hidden : MoneyFormatter.Money(expense.AmountMinor, expense.CurrencyCode),
            expense.Category?.Color ?? Uncategorized.Color, Initial(category));
    }

    private static string Initial(string name) =>
        name.Length == 0 ? "?" : StringInfo.GetNextTextElement(name).ToUpperInvariant();
}

public sealed record ChartBar(string Label, string Amount, string Percent, double Ratio, string Color, bool HasBudget, string BudgetText, double BudgetProgress, string BudgetColor);

/// <summary>Part d'un anneau de répartition : <paramref name="Share"/> entre 0 et 1, couleur hexadécimale.</summary>
public sealed record DonutSlice(double Share, string Color);

/// <summary>Une paire de barres du flux de trésorerie : hauteurs en unités d'écran.</summary>
public sealed record CashflowBar(string Label, double IncomeHeight, double ExpenseHeight);

public sealed record NotificationRow(string Key, string Title, string Message, string Icon, string Severity, bool IsNew);

public sealed record SubscriptionRow(Guid Id, string Name, string Amount, string Frequency, string NextDate);

public sealed record ActiveFilterChip(string Key, string Label);

public sealed record IncomeRow(Guid Id, string Description, string Date, string Amount);

public sealed record RecurringIncomeRow(Guid Id, string Name, string Amount, string Frequency, string NextDate);

public sealed record SavingsGoalRow(Guid Id, string Name, string Saved, string Target, string ProgressText, double Progress);

public sealed record OperationRow(string Type, string Description, string Date, string Amount, string Color);

public sealed record CategoryItem(Guid Id, string Name, string Color, long? BudgetMinor)
{
    internal static CategoryItem From(Category category) => new(category.Id, category.Name, category.Color, category.BudgetMinor);
}

public partial class CategoryFilterOption(Guid id, string name) : ObservableObject
{
    public Guid Id { get; } = id;
    public string Name { get; } = name;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    [NotifyPropertyChangedFor(nameof(AccessibilityDescription))]
    private bool isSelected;
    public string DisplayName => IsSelected ? $"✓ {Name}" : Name;
    public string AccessibilityDescription => IsSelected ? $"{Name}, sélectionnée" : $"{Name}, non sélectionnée";
}

public partial class ExpenseCategoryOption(Guid id, string name, string color) : ObservableObject
{
    public Guid Id { get; } = id;
    public string Name { get; } = name;
    public string Color { get; } = color;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionStroke))]
    [NotifyPropertyChangedFor(nameof(SelectionStrokeThickness))]
    [NotifyPropertyChangedFor(nameof(SelectionMark))]
    [NotifyPropertyChangedFor(nameof(AccessibilityDescription))]
    private bool isSelected;
    public string SelectionStroke => IsSelected ? Palette.SelectionStroke : Palette.Transparent;
    public double SelectionStrokeThickness => IsSelected ? 3 : 0;
    public string SelectionMark => IsSelected ? "✓" : string.Empty;
    public string AccessibilityDescription => IsSelected ? $"{Name}, sélectionnée" : $"{Name}, non sélectionnée";
}
