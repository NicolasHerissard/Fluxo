using System.Globalization;

namespace Finance.Android17.Application;

/// <summary>Lecture d'un montant saisi (« 12,5 », « 12.50 », « -3 ») : renvoie des centimes, arrondis à l'entier supérieur en valeur absolue.</summary>
public static class AmountParser
{
    private static readonly decimal MaxMajorUnits = long.MaxValue / 100m;

    public static bool TryParse(string? text, out long minor)
    {
        minor = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (!decimal.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)) return false;
        if (Math.Abs(parsed) > MaxMajorUnits) return false;
        minor = (long)Math.Round(parsed * 100m, MidpointRounding.AwayFromZero);
        return true;
    }
}
