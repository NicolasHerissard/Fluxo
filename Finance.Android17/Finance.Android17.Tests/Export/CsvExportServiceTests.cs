using System.Text;
using Finance.Android17.Application;
using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Export;

public sealed class CsvExportServiceTests : IDisposable
{
    private readonly InfrastructureHost _host = new();

    public void Dispose() => _host.Dispose();

    private static string Decode(byte[] content)
    {
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, content[..3]);
        return Encoding.UTF8.GetString(content, 3, content.Length - 3);
    }

    [Fact]
    public async Task Export_OnAnEmptyDatabase_SharesTheHeaderOnly()
    {
        await _host.Get<IExportService>().ExportExpensesCsvAsync();

        var file = Assert.Single(_host.Files.Shared);
        Assert.Equal("depenses-20261020-0930.csv", file.FileName);
        Assert.Equal("Exporter les dépenses", file.Title);
        Assert.Equal("Date;Description;Catégorie;Montant;Devise\r\n", Decode(file.Content));
    }

    [Fact]
    public async Task Export_WritesOneLinePerExpense_NewestFirst()
    {
        var store = _host.Get<IFinanceStore>();
        var categories = await store.GetCategoriesAsync();
        var food = categories.Single(x => x.Name == "Alimentation");
        var transport = categories.Single(x => x.Name == "Transport");
        await store.AddExpenseAsync(1_250, "Pain", new DateTime(2026, 10, 2), food.Id);
        await store.AddExpenseAsync(4_005, "Train \"Intercités\"; billet", new DateTime(2026, 10, 15), transport.Id, "USD");

        await _host.Get<IExportService>().ExportExpensesCsvAsync();

        var lines = Decode(_host.Files.Shared.Single().Content).Split("\r\n");
        Assert.Equal(
        [
            "Date;Description;Catégorie;Montant;Devise",
            "2026-10-15;\"Train \"\"Intercités\"\"; billet\";\"Transport\";40.05;\"USD\"",
            "2026-10-02;\"Pain\";\"Alimentation\";12.50;\"EUR\"",
            string.Empty
        ], lines);
    }

    [Fact]
    public async Task Export_IgnoresDeletedExpenses()
    {
        var store = _host.Get<IFinanceStore>();
        var food = (await store.GetCategoriesAsync()).Single(x => x.Name == "Alimentation");
        await store.AddExpenseAsync(1_250, "Pain", new DateTime(2026, 10, 2), food.Id);
        await store.SoftDeleteExpenseAsync((await store.GetExpensesAsync()).Single().Id);

        await _host.Get<IExportService>().ExportExpensesCsvAsync();

        Assert.Equal("Date;Description;Catégorie;Montant;Devise\r\n", Decode(_host.Files.Shared.Single().Content));
    }
}
