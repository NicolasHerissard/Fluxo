using Finance.Android17.Domain;
using System.Globalization;

namespace Finance.Android17.Presentation;

/// <summary>Affichage des montants, pourcentages et dates, toujours au format français quelle que soit la langue de l'appareil.</summary>
public static class MoneyFormatter
{
    public static CultureInfo French { get; } = CultureInfo.GetCultureInfo("fr-FR");

    /// <summary>Remplace un montant quand l'utilisateur a masqué les montants.</summary>
    public const string Hidden = "••••";

    /// <summary>« 1 234,50 € ».</summary>
    public static string Money(long minor, string currencyCode = CurrencyCodes.Default) =>
        $"{MinorUnits.ToMajor(minor).ToString("N2", French)} {CurrencySymbol(currencyCode)}";

    public static string CurrencySymbol(string currencyCode) => currencyCode.ToUpperInvariant() switch
    {
        "USD" => "$",
        "GBP" => "£",
        "CHF" => "CHF",
        "CAD" => "CA$",
        "JPY" => "¥",
        _ => "€"
    };

    /// <summary>Montant à recopier dans un champ de saisie de dépense : « 12,50 ».</summary>
    public static string FixedAmount(long minor) => MinorUnits.ToMajor(minor).ToString("0.00", French);

    /// <summary>Montant à recopier dans un champ de budget, de solde ou d'objectif : « 12,5 », « 200 ».</summary>
    public static string PlainAmount(long minor) => MinorUnits.ToMajor(minor).ToString("0.##", French);

    /// <summary>« 80 % ».</summary>
    public static string Percent(double ratio) => ratio.ToString("P0", French);

    /// <summary>« 02 oct. 2026 ».</summary>
    public static string Date(DateTime date) => date.ToString("dd MMM yyyy", French);
}
