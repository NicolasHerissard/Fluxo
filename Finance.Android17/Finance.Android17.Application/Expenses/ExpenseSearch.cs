using System.Globalization;
using System.Text;
using Finance.Android17.Domain;

namespace Finance.Android17.Application;

/// <summary>
/// Recherche, filtres et tri de la liste des dépenses. La recherche ignore la casse et les accents, exige tous les
/// mots saisis et porte sur la description, la catégorie, le montant (« 12,50 » ou « 1250 ») et la date
/// (« 02/10/2026 » ou « 02 octobre 2026 »), toujours au format français.
/// </summary>
public static class ExpenseSearch
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    public static IReadOnlyList<Expense> Apply(IEnumerable<Expense> expenses, ExpenseCriteria criteria) =>
        Sort(Filter(expenses, criteria), criteria.Sort);

    public static int Count(IEnumerable<Expense> expenses, ExpenseCriteria criteria) => Filter(expenses, criteria).Count;

    public static IReadOnlyList<Expense> Filter(IEnumerable<Expense> expenses, ExpenseCriteria criteria)
    {
        var list = expenses;
        if (criteria.CategoryIds is { Count: > 0 } categoryIds) list = list.Where(x => categoryIds.Contains(x.CategoryId));
        if (criteria.From is DateTime from) list = list.Where(x => x.OccurredOn.Date >= from.Date);
        if (criteria.To is DateTime to) list = list.Where(x => x.OccurredOn.Date <= to.Date);
        if (criteria.MinimumMinor is long minimum) list = list.Where(x => x.AmountMinor >= minimum);
        if (criteria.MaximumMinor is long maximum) list = list.Where(x => x.AmountMinor <= maximum);
        if (!string.IsNullOrWhiteSpace(criteria.SearchText))
        {
            var terms = Normalize(criteria.SearchText).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            list = list.Where(x =>
            {
                var text = SearchableText(x);
                return terms.All(term => text.Contains(term, StringComparison.Ordinal));
            });
        }
        return list.ToList();
    }

    private static IReadOnlyList<Expense> Sort(IEnumerable<Expense> list, ExpenseSort sort)
    {
        var names = StringComparer.Create(French, ignoreCase: true);
        return (sort switch
        {
            ExpenseSort.Oldest => list.OrderBy(x => x.OccurredOn).ThenBy(x => x.CreatedAtUtc),
            ExpenseSort.AmountDescending => list.OrderByDescending(x => x.AmountMinor).ThenByDescending(x => x.OccurredOn),
            ExpenseSort.AmountAscending => list.OrderBy(x => x.AmountMinor).ThenByDescending(x => x.OccurredOn),
            ExpenseSort.NameAscending => list.OrderBy(x => x.Description, names),
            ExpenseSort.NameDescending => list.OrderByDescending(x => x.Description, names),
            _ => list.OrderByDescending(x => x.OccurredOn).ThenByDescending(x => x.CreatedAtUtc)
        }).ToList();
    }

    private static string SearchableText(Expense expense)
    {
        var amount = MinorUnits.ToMajor(expense.AmountMinor).ToString("0.00", French);
        return Normalize($"{expense.Description} {expense.Category?.Name} {amount} {expense.AmountMinor} {expense.OccurredOn.ToString("dd/MM/yyyy", French)} {expense.OccurredOn.ToString("dd MMMM yyyy", French)}");
    }

    private static string Normalize(string value)
    {
        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return string.Concat(decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).Normalize(NormalizationForm.FormC);
    }
}
