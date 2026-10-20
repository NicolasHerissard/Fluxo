using System.Globalization;
using System.Text;
using Finance.Android17.Application;
using Finance.Android17.Domain;

namespace Finance.Android17.Maui;

public sealed class CsvExportService : IExportService
{
    public async Task ExportExpensesCsvAsync(IEnumerable<Expense> expenses, CancellationToken cancellationToken = default)
    {
        var csv = new StringBuilder("Date;Description;Catégorie;Montant;Devise\r\n");
        foreach (var item in expenses)
        {
            csv.Append(item.OccurredOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(';')
                .Append(Escape(item.Description)).Append(';').Append(Escape(item.Category?.Name ?? string.Empty)).Append(';')
                .Append((item.AmountMinor / 100m).ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
                .Append(Escape(item.CurrencyCode)).Append("\r\n");
        }
        var path = Path.Combine(FileSystem.CacheDirectory, $"depenses-{DateTime.Now:yyyyMMdd-HHmm}.csv");
        await File.WriteAllTextAsync(path, csv.ToString(), new UTF8Encoding(true), cancellationToken);
        await Share.Default.RequestAsync(new ShareFileRequest("Exporter les dépenses", new ShareFile(path)));
    }

    private static string Escape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
