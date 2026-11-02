using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Android17.Application;

namespace Finance.Android17.Presentation;

public partial class DashboardViewModel(DashboardService dashboard, ISettingsService settings, TimeProvider clock) : ObservableObject
{
    private const string WeekLabel = "Semaine";
    private const string MonthLabel = "Mois";
    private const string YearLabel = "Année";
    private const string HiddenTotal = "••••••";

    /// <summary>Catégories détaillées dans la légende du graphique ; les suivantes sont regroupées dans « Autres ».</summary>
    private const int LegendCategories = 4;

    /// <summary>Couleur de la part « Autres » du graphique par catégorie.</summary>
    private const string OthersColor = "#A78BFA";

    /// <summary>Hauteur (en unités d'écran) de la plus grande barre du flux de trésorerie.</summary>
    private const double CashflowBarHeight = 112;

    private bool _hasLoaded;

    /// <summary>Les périodes proposées par le sélecteur de l'accueil, dans l'ordre d'affichage.</summary>
    public IReadOnlyList<string> Periods { get; } = [WeekLabel, MonthLabel, YearLabel];
    public ObservableCollection<ExpenseRow> RecentExpenses { get; } = [];
    public ObservableCollection<ChartBar> CategoryBars { get; } = [];
    public ObservableCollection<DonutSlice> CategorySlices { get; } = [];
    public ObservableCollection<CashflowBar> CashflowBars { get; } = [];
    [ObservableProperty] private string selectedPeriod = MonthLabel;
    [ObservableProperty] private string greeting = "Bonjour";
    [ObservableProperty] private string currentDate = string.Empty;
    [ObservableProperty] private string periodTitle = "Ce mois";
    [ObservableProperty] private string total = "0,00 €";
    [ObservableProperty] private string comparison = "Pas encore de comparaison";
    [ObservableProperty] private bool comparisonIsGood;
    [ObservableProperty] private bool comparisonIsBad;
    [ObservableProperty] private string expenseCount = "0 dépense";
    [ObservableProperty] private string topCategory = "—";
    [ObservableProperty] private string nextSubscription = "Aucun abonnement";
    [ObservableProperty] private string currentBalance = "0,00 €";
    [ObservableProperty] private string monthlyIncome = "0,00 €";
    [ObservableProperty] private string monthlyNet = "0,00 €";
    [ObservableProperty] private string forecastBalance = "0,00 €";
    [ObservableProperty] private string forecastStatus = "Après échéances connues";
    [ObservableProperty] private string forecastBadge = "En bonne voie";
    [ObservableProperty] private bool forecastIsHealthy = true;
    [ObservableProperty] private string savingsRate = "0 %";
    [ObservableProperty] private string budgetStatus = string.Empty;
    [ObservableProperty] private double budgetProgress;
    [ObservableProperty] private bool isBudgetVisible;
    [ObservableProperty] private string budgetColor = Palette.BudgetInitial;
    [ObservableProperty] private bool isEmpty;
    [ObservableProperty] private bool hasData;
    [ObservableProperty] private bool hasCashflow;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isSkeletonVisible = true;
    [ObservableProperty] private bool isDashboardVisible;
    [ObservableProperty] private string privacyIcon = "eye";

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        IsSkeletonVisible = !_hasLoaded;
        IsDashboardVisible = _hasLoaded;
        try
        {
            var french = MoneyFormatter.French;
            var now = clock.GetLocalNow();
            Greeting = now.Hour < 18 ? "Bonjour" : "Bonsoir";
            CurrentDate = french.TextInfo.ToTitleCase(now.Date.ToString("dddd d MMMM", french));

            var period = ToPeriod(SelectedPeriod);
            var report = await dashboard.BuildAsync(period);
            var hide = settings.HideAmounts;
            var currency = settings.CurrencyCode;
            string Amount(long minor) => hide ? MoneyFormatter.Hidden : MoneyFormatter.Money(minor, currency);

            var financial = report.Financial;
            CurrentBalance = Amount(financial.CurrentBalanceMinor);
            MonthlyIncome = Amount(financial.IncomeTotalMinor);
            MonthlyNet = Amount(financial.NetTotalMinor);
            ForecastBalance = Amount(financial.ForecastBalanceMinor);
            ForecastIsHealthy = financial.ForecastBalanceMinor >= 0;
            ForecastBadge = ForecastIsHealthy ? "En bonne voie" : "À surveiller";
            ForecastStatus = ForecastIsHealthy ? "Après échéances connues" : "Découvert probable en fin de mois";
            SavingsRate = MoneyFormatter.Percent(financial.SavingsRate);

            PeriodTitle = period switch { DashboardPeriod.Week => "Cette semaine", DashboardPeriod.Year => "Cette année", _ => "Ce mois" };
            Total = hide ? HiddenTotal : MoneyFormatter.Money(report.TotalMinor, currency);
            PrivacyIcon = hide ? "eye-off" : "eye";
            ExpenseCount = $"{report.ExpenseCount} dépense{(report.ExpenseCount > 1 ? "s" : string.Empty)}";
            NextSubscription = report.NextSubscription is { } next
                ? $"{next.Name} · {next.NextBillingDate.ToString("dd MMM", french)}"
                : "Aucun abonnement";
            TopCategory = report.TopCategoryName ?? "—";

            CategoryBars.Clear();
            foreach (var spend in report.Categories.Take(LegendCategories))
            {
                var budget = spend.Budget;
                var budgetText = budget is null ? string.Empty
                    : $"Budget : {Amount(spend.TotalMinor)} / {Amount(budget.BudgetMinor)}";
                var color = budget?.Level switch { BudgetLevel.Exceeded => Palette.Danger, BudgetLevel.Warning => Palette.Warning, _ => spend.Color };
                CategoryBars.Add(new ChartBar(spend.Name, Amount(spend.TotalMinor), MoneyFormatter.Percent(spend.Share), spend.Share, spend.Color,
                    budget is not null, budgetText, budget?.Progress ?? 0, color));
            }

            CategorySlices.Clear();
            foreach (var spend in report.Categories.Take(LegendCategories)) CategorySlices.Add(new DonutSlice(spend.Share, spend.Color));
            var others = report.Categories.Skip(LegendCategories).Sum(x => x.Share);
            if (others > 0) CategorySlices.Add(new DonutSlice(others, OthersColor));

            RecentExpenses.Clear();
            foreach (var expense in report.RecentExpenses) RecentExpenses.Add(ExpenseRow.From(expense, hide));

            BuildCashflow(report.Cashflow, french);

            IsEmpty = report.ExpenseCount == 0;
            HasData = !IsEmpty;
            IsBudgetVisible = report.MonthlyBudget is not null;
            if (report.MonthlyBudget is { } monthly)
            {
                BudgetProgress = monthly.Progress;
                BudgetStatus = monthly.RemainingMinor >= 0
                    ? $"{Amount(monthly.RemainingMinor)} disponibles sur le budget"
                    : $"Budget dépassé de {Amount(-monthly.RemainingMinor)}";
                BudgetColor = monthly.Level switch { BudgetLevel.Exceeded => Palette.Danger, BudgetLevel.Warning => Palette.Warning, _ => Palette.Good };
            }

            var comparison = report.Comparison;
            var percent = comparison.Percent.ToString("0", french);
            Comparison = comparison.Direction switch
            {
                TrendDirection.NoReference => "Aucune donnée sur la période précédente",
                TrendDirection.Down => $"↓ {percent} % par rapport à la période précédente",
                TrendDirection.Up => $"↑ {percent} % par rapport à la période précédente",
                _ => "→ Identique à la période précédente"
            };
            ComparisonIsGood = comparison.Direction == TrendDirection.Down;
            ComparisonIsBad = comparison.Direction == TrendDirection.Up;
        }
        finally
        {
            _hasLoaded = true;
            IsSkeletonVisible = false;
            IsDashboardVisible = true;
            IsBusy = false;
        }
    }

    private void BuildCashflow(IReadOnlyList<MonthlyFlow> flows, System.Globalization.CultureInfo french)
    {
        CashflowBars.Clear();
        var peak = flows.Select(x => Math.Max(x.ExpenseMinor, x.IncomeMinor)).DefaultIfEmpty(0).Max();
        HasCashflow = peak > 0;
        if (!HasCashflow) return;

        double Height(long minor) => minor <= 0 ? 0 : Math.Max(3, CashflowBarHeight * minor / peak);
        foreach (var flow in flows)
            CashflowBars.Add(new CashflowBar(french.TextInfo.ToTitleCase(flow.Month.ToString("MMM", french).TrimEnd('.')), Height(flow.IncomeMinor), Height(flow.ExpenseMinor)));
    }

    [RelayCommand]
    private async Task SelectPeriodAsync(string period)
    {
        SelectedPeriod = period;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task TogglePrivacyAsync()
    {
        settings.HideAmounts = !settings.HideAmounts;
        await LoadAsync();
    }

    private static DashboardPeriod ToPeriod(string label) => label switch
    {
        WeekLabel => DashboardPeriod.Week,
        YearLabel => DashboardPeriod.Year,
        _ => DashboardPeriod.Month
    };
}
