namespace Finance.Android17.Application;

public interface IExportService
{
    /// <summary>Exporte toutes les dépenses actives en CSV et ouvre la feuille de partage.</summary>
    Task ExportExpensesCsvAsync(CancellationToken cancellationToken = default);
}
