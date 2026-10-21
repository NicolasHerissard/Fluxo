namespace Finance.Android17.Application;

// Valeurs enregistrées telles quelles dans les préférences de l'utilisateur : les modifier casserait le réglage
// déjà choisi sur les appareils existants.

/// <summary>Choix du thème de l'application.</summary>
public static class ThemeOptions
{
    public const string FollowSystem = "Système";
    public const string Light = "Clair";
    public const string Dark = "Sombre";

    public static IReadOnlyList<string> All { get; } = [FollowSystem, Light, Dark];
}

/// <summary>Ce qui se passe après l'enregistrement d'une dépense.</summary>
public static class AfterSaveOptions
{
    public const string Close = "Fermer";
    public const string AddAnother = "Ajouter une autre";

    public static IReadOnlyList<string> All { get; } = [Close, AddAnother];
}
