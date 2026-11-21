namespace Finance.Android17.Application;

/// <summary>
/// Bornes de saisie communes à l'interface et à la validation des sauvegardes : tout ce que l'application accepte de saisir
/// doit pouvoir être restauré depuis un fichier .mfbackup.
/// </summary>
public static class InputLimits
{
    /// <summary>Plafond d'un montant en centimes (10^13 = 100 milliards) : assez haut pour tout usage réel, assez bas pour que les totaux ne débordent jamais.</summary>
    public const long MaxAmountMinor = 10_000_000_000_000;

    public const int MaxDescriptionLength = 240;
    public const int MaxCategoryNameLength = 80;
    public const int MaxNameLength = 120;
}
