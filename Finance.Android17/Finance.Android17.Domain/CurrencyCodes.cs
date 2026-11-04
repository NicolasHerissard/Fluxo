namespace Finance.Android17.Domain;

/// <summary>Devises proposées par l'application et devise par défaut des opérations.</summary>
public static class CurrencyCodes
{
    public const string Default = "EUR";

    public static IReadOnlyList<string> Supported { get; } = [Default, "USD", "CHF", "GBP", "CAD", "JPY"];
}
