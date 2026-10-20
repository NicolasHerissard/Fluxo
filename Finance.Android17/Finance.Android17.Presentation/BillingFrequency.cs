namespace Finance.Android17.Presentation;

/// <summary>Fréquences proposées pour un abonnement ou un revenu récurrent, et leur intervalle en mois.</summary>
public static class BillingFrequency
{
    public const string Monthly = "Mensuel";
    public const string Quarterly = "Trimestriel";
    public const string Yearly = "Annuel";

    public static IReadOnlyList<string> Options { get; } = [Monthly, Quarterly, Yearly];

    public static int ToMonths(string option) => option switch { Quarterly => 3, Yearly => 12, _ => 1 };

    /// <summary>Option à sélectionner pour modifier un élément existant.</summary>
    public static string ToOption(int months) => months switch { 3 => Quarterly, 12 => Yearly, _ => Monthly };

    /// <summary>Libellé affiché dans les listes.</summary>
    public static string ToDisplay(int months) => months switch { 3 => "Tous les 3 mois", 12 => Yearly, _ => Monthly };
}
