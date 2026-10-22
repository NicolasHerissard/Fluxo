using System.Globalization;
using System.Text;
using Finance.Android17.Application;
using Finance.Android17.Domain;

namespace Finance.Android17.Infrastructure;

/// <summary>Export des dépenses : CSV séparé par « ; », UTF-8 avec BOM (lisible directement par Excel).</summary>
internal sealed class CsvExportService(IExpenseRepository expenseRepository, IFileExchange files, TimeProvider clock) : IExportService
{
    private const string Header = "Date;Description;Catégorie;Montant;Devise";

    public async Task ExportExpensesCsvAsync(CancellationToken cancellationToken = default)
    {
        var expenses = await expenseRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var content = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        byte[] bytes = [.. content.GetPreamble(), .. content.GetBytes(Build(expenses))];
        var fileName = $"depenses-{clock.GetLocalNow():yyyyMMdd-HHmm}.csv";
        await files.ShareAsync(fileName, bytes, "Exporter les dépenses", cancellationToken).ConfigureAwait(false);
    }

    private static string Build(IEnumerable<Expense> expenses)
    {
        var csv = new StringBuilder(Header).Append("\r\n");
        foreach (var item in expenses)
        {
            csv.Append(item.OccurredOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(';')
                .Append(Escape(item.Description)).Append(';').Append(Escape(item.Category?.Name ?? string.Empty)).Append(';')
                .Append((item.AmountMinor / 100m).ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
                .Append(Escape(item.CurrencyCode)).Append("\r\n");
        }
        return csv.ToString();
    }

    // Un texte qui commence par = + - @ (ou tabulation / retour chariot) serait évalué comme une formule à l'ouverture
    // dans Excel ou LibreOffice : on le préfixe d'une apostrophe, qui reste visible mais neutralise la formule.
    private static string Escape(string value)
    {
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r') value = "'" + value;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
