namespace Finance.Android17.Domain;

/// <summary>Catégories fournies par l'application : le jeu initial et la catégorie des dépenses d'abonnement.</summary>
public static class DefaultCategories
{
    public const string SubscriptionsName = "Abonnements";
    public const string SubscriptionsColor = "#7D5260";

    /// <summary>Créées au premier lancement, et recréées par « Supprimer toutes les données ».</summary>
    public static IReadOnlyList<Category> CreateInitialSet() =>
    [
        new() { Name = "Alimentation", Color = "#2E7D32" },
        new() { Name = "Transport", Color = "#1565C0" },
        new() { Name = "Logement", Color = "#6A1B9A" },
        new() { Name = "Loisirs", Color = "#EF6C00" },
        new() { Name = "Autre", Color = "#546E7A" }
    ];

    /// <summary>Catégorie des dépenses générées par les abonnements ; créée à la première échéance.</summary>
    public static Category CreateSubscriptions() => new() { Name = SubscriptionsName, Color = SubscriptionsColor };
}
