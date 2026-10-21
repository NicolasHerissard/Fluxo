namespace Finance.Android17.Application;

/// <summary>Données de l'accueil : charge les informations financières et calcule le rapport de la période demandée.</summary>
public sealed class DashboardService(FinanceDataLoader loader, ISettingsService settings, TimeProvider clock)
{
    public async Task<DashboardReport> BuildAsync(DashboardPeriod period, CancellationToken cancellationToken = default)
    {
        var data = await loader.LoadAsync(cancellationToken).ConfigureAwait(false);
        return DashboardCalculator.Build(data, period, clock.GetLocalNow().Date, settings.MonthlyBudgetMinor);
    }
}
