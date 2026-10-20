using Finance.Android17.Domain;

namespace Finance.Android17.Application;

/// <summary>Choix des catégories proposées à la saisie d'une dépense.</summary>
public static class CategoryChoice
{
    public const int RecentExpensesConsidered = 50;

    /// <summary>Les catégories des 50 dépenses les plus récentes d'abord (la plus récemment utilisée en tête), puis les autres par nom.</summary>
    public static IReadOnlyList<Category> OrderByRecentUse(IEnumerable<Category> categories, IEnumerable<Expense> expenses)
    {
        var recentIds = expenses
            .OrderByDescending(x => x.OccurredOn).ThenByDescending(x => x.CreatedAtUtc)
            .Take(RecentExpensesConsidered)
            .GroupBy(x => x.CategoryId)
            .OrderByDescending(g => g.Max(x => x.OccurredOn))
            .Select(g => g.Key)
            .ToList();
        return categories
            .OrderBy(x => recentIds.IndexOf(x.Id) is var index and >= 0 ? index : int.MaxValue)
            .ThenBy(x => x.Name)
            .ToList();
    }

    /// <summary>
    /// Catégorie présélectionnée : celle déjà choisie, sinon la dernière utilisée (si l'utilisateur le souhaite) ou la
    /// catégorie par défaut, sinon la première proposée.
    /// </summary>
    public static Category? Preferred(IReadOnlyList<Category> categories, Guid? currentId, string lastCategoryId, string defaultCategoryId, bool rememberLast)
    {
        Guid?[] candidates = [currentId, Parse(rememberLast ? lastCategoryId : defaultCategoryId), Parse(defaultCategoryId)];
        foreach (var id in candidates)
        {
            var match = id is null ? null : categories.FirstOrDefault(x => x.Id == id);
            if (match is not null) return match;
        }
        return categories.FirstOrDefault();
    }

    private static Guid? Parse(string value) => Guid.TryParse(value, out var id) ? id : null;
}
