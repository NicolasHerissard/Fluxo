using System.Globalization;
using System.Text.RegularExpressions;

namespace Finance.Android17.Application;

public sealed record ReceiptDraft(long? AmountMinor, DateTime? OccurredOn, string? MerchantName, string RawText);

public static partial class ReceiptParser
{
    private static readonly string[] TotalLabels = ["total ttc", "total", "a payer", "à payer", "net à payer", "net a payer"];

    public static ReceiptDraft Parse(string text)
    {
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var amount = FindTotal(lines);
        var date = FindDate(normalized);
        var merchant = lines.FirstOrDefault(x => x.Length >= 2 && !ContainsAmount(x) && !DateRegex().IsMatch(x) && !LooksLikeNoise(x));
        return new(amount, date, merchant, normalized.Trim());
    }

    private static long? FindTotal(IEnumerable<string> lines)
    {
        foreach (var line in lines.Reverse())
        {
            var lower = line.ToLowerInvariant();
            if (TotalLabels.Any(lower.Contains) && TryFindAmount(line, out var amount)) return amount;
        }

        return lines.Reverse().Select(x => TryFindAmount(x, out var amount) ? amount : (long?)null).FirstOrDefault();
    }

    private static DateTime? FindDate(string text)
    {
        foreach (Match match in DateRegex().Matches(text))
        {
            if (DateTime.TryParseExact(match.Value, ["dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
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
            amountMinor = checked((long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero));
            return true;
        }
        amountMinor = 0;
        return false;
    }

    private static bool ContainsAmount(string value) => AmountRegex().IsMatch(value);
    private static bool LooksLikeNoise(string value) => value.Length < 3 || value.All(x => !char.IsLetter(x));

    [GeneratedRegex(@"(?<!\d)(\d{1,3}(?:[ .]\d{3})*(?:[,.]\d{2}))(?:\s*€|\s*EUR)?\b", RegexOptions.IgnoreCase)]
    private static partial Regex AmountRegex();

    [GeneratedRegex(@"\b\d{1,2}[/-]\d{1,2}[/-]\d{2,4}\b")]
    private static partial Regex DateRegex();
}
