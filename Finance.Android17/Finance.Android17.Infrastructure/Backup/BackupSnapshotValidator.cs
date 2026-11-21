using System.Text.RegularExpressions;
using Finance.Android17.Application;

namespace Finance.Android17.Infrastructure;

/// <summary>
/// Contrôle le contenu d'une sauvegarde avant qu'elle ne remplace les données : le chiffrement prouve que le fichier n'a pas
/// été altéré, pas que ses valeurs sont raisonnables (un fichier produit par une autre version ou à la main). Un refus
/// laisse la base intacte et dit en français ce qui ne va pas.
/// </summary>
internal static partial class BackupSnapshotValidator
{
    internal const long MaxAmountMinor = InputLimits.MaxAmountMinor;
    internal const int MaxIntervalMonths = 1_200;
    private static readonly DateTime EarliestDate = new(1900, 1, 1);
    private static readonly DateTime LatestDate = new(2200, 12, 31);

    [GeneratedRegex("^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$")] private static partial Regex ColorPattern();
    [GeneratedRegex("^[A-Z]{3}$")] private static partial Regex CurrencyPattern();

    public static void Validate(FinanceBackupSnapshot snapshot)
    {
        var incomes = snapshot.Incomes ?? [];
        var recurringIncomes = snapshot.RecurringIncomes ?? [];
        var goals = snapshot.SavingsGoals ?? [];

        RequireAmount(snapshot.InitialBalanceMinor, "le solde initial");
        var categoryIds = new HashSet<Guid>();
        foreach (var x in snapshot.Categories)
        {
            Require(categoryIds.Add(x.Id) && x.Id != Guid.Empty, "une catégorie a un identifiant manquant ou en double");
            RequireText(x.Name, InputLimits.MaxCategoryNameLength, "le nom d'une catégorie");
            Require(x.Color is not null && ColorPattern().IsMatch(x.Color), $"la couleur de la catégorie « {x.Name} » n'est pas valide");
            Require(x.BudgetMinor is null or (> 0 and <= MaxAmountMinor), $"le budget de la catégorie « {x.Name} » n'est pas valide");
        }

        RequireUnique(snapshot.Expenses.Select(x => x.Id), "une dépense");
        foreach (var x in snapshot.Expenses)
        {
            RequireAmount(x.AmountMinor, "le montant d'une dépense");
            RequireCurrency(x.CurrencyCode);
            RequireText(x.Description, InputLimits.MaxDescriptionLength, "la description d'une dépense", allowEmpty: true);
            RequireDate(x.OccurredOn, "la date d'une dépense");
            Require(categoryIds.Contains(x.CategoryId), $"la dépense « {x.Description} » renvoie à une catégorie absente de la sauvegarde");
        }

        RequireUnique(snapshot.Subscriptions.Select(x => x.Id), "un abonnement");
        foreach (var x in snapshot.Subscriptions)
        {
            RequireText(x.Name, InputLimits.MaxNameLength, "le nom d'un abonnement");
            RequireAmount(x.AmountMinor, $"le montant de l'abonnement « {x.Name} »");
            RequireCurrency(x.CurrencyCode);
            RequireInterval(x.BillingIntervalMonths, x.Name);
            RequireDate(x.NextBillingDate, $"l'échéance de l'abonnement « {x.Name} »");
            RequireAnchorDay(x.AnchorDay, x.Name);
        }

        RequireUnique(incomes.Select(x => x.Id), "un revenu");
        foreach (var x in incomes)
        {
            RequireAmount(x.AmountMinor, "le montant d'un revenu");
            RequireCurrency(x.CurrencyCode);
            RequireText(x.Description, InputLimits.MaxDescriptionLength, "la description d'un revenu", allowEmpty: true);
            RequireDate(x.OccurredOn, "la date d'un revenu");
        }

        RequireUnique(recurringIncomes.Select(x => x.Id), "un revenu récurrent");
        foreach (var x in recurringIncomes)
        {
            RequireText(x.Name, InputLimits.MaxNameLength, "le nom d'un revenu récurrent");
            RequireAmount(x.AmountMinor, $"le montant du revenu récurrent « {x.Name} »");
            RequireCurrency(x.CurrencyCode);
            RequireInterval(x.IntervalMonths, x.Name);
            RequireDate(x.NextOccurrenceDate, $"l'échéance du revenu récurrent « {x.Name} »");
            RequireAnchorDay(x.AnchorDay, x.Name);
        }

        RequireUnique(goals.Select(x => x.Id), "un objectif d'épargne");
        foreach (var x in goals)
        {
            RequireText(x.Name, InputLimits.MaxNameLength, "le nom d'un objectif d'épargne");
            Require(x.TargetAmountMinor is > 0 and <= MaxAmountMinor, $"le montant cible de l'objectif « {x.Name} » n'est pas valide");
            Require(x.SavedAmountMinor is >= 0 and <= MaxAmountMinor, $"le montant épargné de l'objectif « {x.Name} » n'est pas valide");
            if (x.TargetDate is { } targetDate) RequireDate(targetDate, $"la date cible de l'objectif « {x.Name} »");
        }
    }

    private static void Require(bool condition, string problem)
    {
        if (!condition) throw new InvalidOperationException($"Sauvegarde refusée : {problem}. Vos données n'ont pas été modifiées.");
    }

    private static void RequireUnique(IEnumerable<Guid> ids, string what)
    {
        var seen = new HashSet<Guid>();
        foreach (var id in ids) Require(id != Guid.Empty && seen.Add(id), $"{what} a un identifiant manquant ou en double");
    }

    private static void RequireText(string? value, int maxLength, string what, bool allowEmpty = false) =>
        Require(value is not null && value.Length <= maxLength && (allowEmpty || !string.IsNullOrWhiteSpace(value)), $"{what} est vide ou trop long");

    private static void RequireAmount(long amountMinor, string what) =>
        Require(amountMinor is >= -MaxAmountMinor and <= MaxAmountMinor, $"{what} dépasse les limites");

    private static void RequireCurrency(string? code) =>
        Require(code is not null && CurrencyPattern().IsMatch(code), "un code de devise n'est pas valide");

    private static void RequireInterval(int months, string name) =>
        Require(months is >= 1 and <= MaxIntervalMonths, $"la périodicité de « {name} » n'est pas valide");

    private static void RequireAnchorDay(int anchorDay, string name) =>
        Require(anchorDay is >= 0 and <= 31, $"le jour d'échéance de « {name} » n'est pas valide");

    private static void RequireDate(DateTime date, string what) =>
        Require(date >= EarliestDate && date <= LatestDate, $"{what} est hors des limites");
}
