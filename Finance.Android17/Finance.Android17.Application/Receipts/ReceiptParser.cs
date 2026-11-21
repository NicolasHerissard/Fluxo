using Finance.Android17.Domain;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Finance.Android17.Application;

public sealed record ReceiptDraft(long? AmountMinor, DateTime? OccurredOn, string? MerchantName, string RawText);

public static partial class ReceiptParser
{
    private static readonly string[] TotalLabels = ["total ttc", "total", "a payer", "à payer", "net à payer", "net a payer", "carte bancaire", "montant"];

    public static ReceiptDraft Parse(string text)
    {
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var amount = FindTotal(lines);
        var date = FindDate(normalized);
        var merchant = lines.FirstOrDefault(x => x.Length >= 2 && !ContainsAmount(x) && !DateRegex().IsMatch(x) && !LooksLikeNoise(x));
        return new(amount, date, merchant, normalized.Trim());
    }

    private static readonly string[] IgnoredTotalMarkers = ["sous-total", "sous total", "tva", "remise", "rendu", "monnaie", "espèces", "especes", "articles"];

    private static long? FindTotal(IEnumerable<string> allLines)
    {
        var lines = allLines.ToArray();
        for (var i = lines.Length - 1; i >= 0; i--)
        {
            var lower = lines[i].ToLowerInvariant();
            if (IgnoredTotalMarkers.Any(lower.Contains) || !TotalLabels.Any(lower.Contains)) continue;
            if (TryFindAmount(lines[i], out var amount)) return amount;
            // Le montant est parfois seul sur la ligne suivante.
            if (i + 1 < lines.Length && TryFindAmount(lines[i + 1], out amount)) return amount;
        }

        for (var i = lines.Length - 1; i >= 0; i--)
        {
            var lower = lines[i].ToLowerInvariant();
            if (IgnoredTotalMarkers.Any(lower.Contains)) continue;
            if (TryFindAmount(lines[i], out var amount)) return amount;
        }

        return null;
    }

    private static DateTime? FindDate(string text)
    {
        foreach (Match match in DateRegex().Matches(text))
        {
            var value = match.Value.Replace('-', '/').Replace('.', '/');
            if (DateTime.TryParseExact(value, ["dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "d/M/yy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                return date.Date;
        }
        return null;
    }

    private static bool TryFindAmount(string value, out long amountMinor)
    {
        foreach (Match match in AmountRegex().Matches(value))
        {
            var number = match.Groups[1].Value.Replace(" ", string.Empty).Replace(',', '.');
            if (!decimal.TryParse(number, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)) continue;
            if (amount <= 0 || amount > 100_000) continue;
            amountMinor = checked((long)Math.Round(amount * MinorUnits.PerMajor, MidpointRounding.AwayFromZero));
            return true;
        }
        amountMinor = 0;
        return false;
    }

    private static bool ContainsAmount(string value) => AmountRegex().IsMatch(value);
    private static bool LooksLikeNoise(string value) => value.Length < 3 || value.All(x => !char.IsLetter(x));

    [GeneratedRegex(@"(?<!\d)(\d{1,3}(?:[ .]\d{3})*(?:[,.]\d{2}))(?:\s*€|\s*EUR)?\b", RegexOptions.IgnoreCase)]
    private static partial Regex AmountRegex();

    [GeneratedRegex(@"\b\d{1,2}[/.-]\d{1,2}[/.-]\d{2,4}\b")]
    private static partial Regex DateRegex();
}
