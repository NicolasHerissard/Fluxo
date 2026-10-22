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

    private bool _hasLoaded;
    public ObservableCollection<ExpenseRow> RecentExpenses { get; } = [];
    public ObservableCollection<ChartBar> CategoryBars { get; } = [];
    [ObservableProperty] private string selectedPeriod = MonthLabel;
    [ObservableProperty] private string greeting = "Bonjour";
    [ObservableProperty] private string currentDate = string.Empty;
    [ObservableProperty] private string periodTitle = "Ce mois";
    [ObservableProperty] private string total = "0,00 €";
    [ObservableProperty] private string comparison = "Pas encore de comparaison";
    [ObservableProperty] private string comparisonColor = Palette.Neutral;
    [ObservableProperty] private string expenseCount = "0 dépense";
    [ObservableProperty] private string topCategory = "—";
    [ObservableProperty] private string nextSubscription = "Aucun abonnement";
    [ObservableProperty] private string currentBalance = "0,00 €";
    [ObservableProperty] private string monthlyIncome = "0,00 €";
    [ObservableProperty] private string monthlyNet = "0,00 €";
    [ObservableProperty] private string forecastBalance = "0,00 €";
    [ObservableProperty] private string forecastStatus = "Après échéances connues";
    [ObservableProperty] private string savingsRate = "0 %";
    [ObservableProperty] private string budgetStatus = string.Empty;
    [ObservableProperty] private double budgetProgress;
    [ObservableProperty] private bool isBudgetVisible;
    [ObservableProperty] private string budgetColor = Palette.BudgetInitial;
    [ObservableProperty] private bool isEmpty;
    [ObservableProperty] private bool hasData;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isSkeletonVisible = true;
    [ObservableProperty] private bool isDashboardVisible;
    [ObservableProperty] private string privacyIcon = "icon_visibility.png";
    [ObservableProperty] private string weekBackground = Palette.Transparent;
    [ObservableProperty] private string monthBackground = Palette.Primary;
    [ObservableProperty] private string yearBackground = Palette.Transparent;
    [ObservableProperty] private string weekTextColor = Palette.OnPrimaryMuted;
    [ObservableProperty] private string monthTextColor = Palette.OnPrimary;
    [ObservableProperty] private string yearTextColor = Palette.OnPrimaryMuted;

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
            ForecastStatus = financial.ForecastBalanceMinor < 0 ? "Découvert probable en fin de mois" : "Après échéances connues";
            SavingsRate = MoneyFormatter.Percent(financial.SavingsRate);

            PeriodTitle = period switch { DashboardPeriod.Week => "Cette semaine", DashboardPeriod.Year => "Cette année", _ => "Ce mois" };
            Total = hide ? HiddenTotal : MoneyFormatter.Money(report.TotalMinor, currency);
            PrivacyIcon = hide ? "icon_visibility_off.png" : "icon_visibility.png";
            ExpenseCount = $"{report.ExpenseCount} dépense{(report.ExpenseCount > 1 ? "s" : string.Empty)}";
            NextSubscription = report.NextSubscription is { } next
                ? $"{next.Name} · {next.NextBillingDate.ToString("dd MMM", french)}"
                : "Aucun abonnement";
            TopCategory = report.TopCategoryName ?? "—";

            CategoryBars.Clear();
            foreach (var spend in report.Categories.Take(3))
            {
                var budget = spend.Budget;
                var budgetText = budget is null ? string.Empty
                    : $"Budget : {Amount(spend.TotalMinor)} / {Amount(budget.BudgetMinor)}";
                var color = budget?.Level switch { BudgetLevel.Exceeded => Palette.Danger, BudgetLevel.Warning => Palette.Warning, _ => spend.Color };
                CategoryBars.Add(new ChartBar(spend.Name, Amount(spend.TotalMinor), MoneyFormatter.Percent(spend.Share), spend.Share, spend.Color,
                    budget is not null, budgetText, budget?.Progress ?? 0, color));
            }

            RecentExpenses.Clear();
            foreach (var expense in report.RecentExpenses) RecentExpenses.Add(ExpenseRow.From(expense, hide));

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
            (Comparison, ComparisonColor) = comparison.Direction switch
            {
                TrendDirection.NoReference => ("Aucune donnée sur la période précédente", Palette.Neutral),
                TrendDirection.Down => ($"↓ {percent} % par rapport à la période précédente", Palette.Good),
                TrendDirection.Up => ($"↑ {percent} % par rapport à la période précédente", Palette.DangerSoft),
                _ => ("→ Identique à la période précédente", Palette.Neutral)
            };
        }
        finally
        {
            _hasLoaded = true;
            IsSkeletonVisible = false;
            IsDashboardVisible = true;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SelectPeriodAsync(string period)
    {
        SelectedPeriod = period;
        WeekBackground = period == WeekLabel ? Palette.Primary : Palette.Transparent;
        MonthBackground = period == MonthLabel ? Palette.Primary : Palette.Transparent;
        YearBackground = period == YearLabel ? Palette.Primary : Palette.Transparent;
        WeekTextColor = period == WeekLabel ? Palette.OnPrimary : Palette.OnPrimaryMuted;
        MonthTextColor = period == MonthLabel ? Palette.OnPrimary : Palette.OnPrimaryMuted;
        YearTextColor = period == YearLabel ? Palette.OnPrimary : Palette.OnPrimaryMuted;
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
