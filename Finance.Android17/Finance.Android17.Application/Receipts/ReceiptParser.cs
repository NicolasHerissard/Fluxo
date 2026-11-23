using Finance.Android17.Domain;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Finance.Android17.Application;

public sealed record ReceiptDraft(long? AmountMinor, DateTime? OccurredOn, string? MerchantName, string RawText);

/// <summary>
/// Extrait le montant payé, la date et le commerçant du texte d'un ticket (OCR). Le montant visé est le « total à payer » :
/// libellé explicite d'abord, puis montant du paiement (carte), puis « total » générique, puis un recoupement par répétition.
/// Les lignes de TVA, de base HT, de sous-totaux (« Total Alimentaire »), de rendu monnaie ou de fidélité sont ignorées.
/// </summary>
public static partial class ReceiptParser
{
    // Textes comparés sans accents ni majuscules.
    private static readonly string[] PayableLabels =
        ["a payer", "a regler", "total ttc", "montant ttc", "montant du", "net ttc", "total general", "total net", "total regle"];

    private static readonly string[] MerchantNoise =
        ["bienvenue", "merci", "ticket", "facture", "duplicata", "tel", "www", "siret", "caisse", "client"];

    public static ReceiptDraft Parse(string text)
    {
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var amount = FindTotal(lines);
        var date = FindDate(normalized);
        var merchant = lines.FirstOrDefault(x => IsMerchantCandidate(x));
        return new(amount, date, merchant, normalized.Trim());
    }

    private static bool IsMerchantCandidate(string line)
    {
        if (line.Length < 2 || ContainsAmount(line) || DateRegex().IsMatch(line) || LooksLikeNoise(line)) return false;
        var folded = Fold(line);
        return !MerchantNoise.Any(noise => folded.StartsWith(noise, StringComparison.Ordinal));
    }

    private static long? FindTotal(string[] lines)
    {
        var folded = lines.Select(Fold).ToArray();
        var payable = new List<long>();
        var payments = new List<long>();
        var generic = new List<long>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = folded[i];
            if (ExcludedRegex().IsMatch(line)) continue;
            var isPayable = PayableLabels.Any(line.Contains);
            var isPayment = PaymentRegex().IsMatch(line);
            var isGeneric = TotalWordRegex().IsMatch(line);
            if (!isPayable && !isPayment && !isGeneric) continue;
            if (!TryFindAmountNear(lines, i, out var amount)) continue;
            if (isPayable) payable.Add(amount);
            else if (isPayment) payments.Add(amount);
            else generic.Add(amount);
        }

        if (payable.Count > 0)
        {
            // Un montant confirmé par le paiement (même valeur sur la ligne carte) est le plus sûr ; sinon le dernier libellé.
            var confirmed = payable.LastOrDefault(payments.Contains);
            return confirmed != 0 ? confirmed : payable[^1];
        }

        if (payments.Count > 0) return payments.Max();
        if (generic.Count > 0) return generic[^1];

        // Aucun libellé reconnu : un montant qui revient plusieurs fois (total puis paiement) est probablement le total.
        var candidates = new List<long>();
        for (var i = 0; i < lines.Length; i++)
        {
            if (ExcludedRegex().IsMatch(folded[i]) || folded[i].StartsWith("tel", StringComparison.Ordinal)) continue;
            if (TryFindAmount(lines[i], out var amount)) candidates.Add(amount);
        }

        if (candidates.Count == 0) return null;
        var repeated = candidates.GroupBy(x => x).Where(g => g.Count() >= 2).Select(g => g.Key).ToList();
        return repeated.Count > 0 ? repeated.Max() : candidates.Max();
    }

    /// <summary>Le montant est sur la ligne du libellé ou, selon l'ordre de lecture de l'OCR, seul sur l'une des deux lignes suivantes.</summary>
    private static bool TryFindAmountNear(string[] lines, int index, out long amountMinor)
    {
        if (TryFindAmount(lines[index], out amountMinor)) return true;
        for (var next = index + 1; next <= index + 2 && next < lines.Length; next++)
        {
            if (!AmountOnlyRegex().IsMatch(lines[next])) break;
            if (TryFindAmount(lines[next], out amountMinor)) return true;
        }

        amountMinor = 0;
        return false;
    }

    private static string Fold(string value)
    {
        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) builder.Append(c);
        return builder.ToString();
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

    // Lignes qui ne portent jamais le total payé : TVA, base HT, sous-totaux, catégories, remises, rendu, espèces, fidélité.
    [GeneratedRegex(@"\b(tva|ht|sous[- ]?total|alimentaire|remises?|economies?|fidelite|cagnotte|rendu|monnaie|especes?|points?)\b")]
    private static partial Regex ExcludedRegex();

    [GeneratedRegex(@"\b(cartes? bancaires?|carte bleue|cb|visa|mastercard|sans contact|paiement)\b")]
    private static partial Regex PaymentRegex();

    [GeneratedRegex(@"\btotal\b")]
    private static partial Regex TotalWordRegex();

    [GeneratedRegex(@"^[\s€]*(?:\d{1,3}(?:[ .]\d{3})*[,.]\d{2})\s*(?:€|eur)?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex AmountOnlyRegex();
}
