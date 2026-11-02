namespace Finance.Android17.Presentation;

/// <summary>
/// Couleurs que les ViewModels transmettent aux vues sous forme de texte. Ce sont des tons moyens, lisibles aussi
/// bien sur une carte claire que sur une carte sombre (barres de progression, pastilles) : un ViewModel ne connaît
/// pas le thème. Les textes qui doivent changer de teinte avec le thème passent par un indicateur booléen que la
/// vue associe à une couleur du thème.
/// </summary>
internal static class Palette
{
    /// <summary>Couleur suggérée en premier pour une nouvelle catégorie (celle des catégories existantes sans couleur).</summary>
    public const string Primary = "#6750A4";
    public const string Transparent = "Transparent";

    public const string Good = "#22A06B";
    public const string Warning = "#F59E0B";
    public const string Danger = "#F43F5E";
    public const string BudgetInitial = Good;

    public const string SelectionStroke = "#818CF8";
}
