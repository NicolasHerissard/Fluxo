using System.Globalization;

namespace Finance.Android17.Application;

/// <summary>Règles du centre de notifications : budgets proches ou dépassés, découvert probable, prélèvements à venir.</summary>
public static class NotificationBuilder
{
    /// <summary>Un prélèvement est annoncé quand son échéance tombe dans ce nombre de jours (aujourd'hui compris).</summary>
    public const int UpcomingDays = 7;

    /// <param name="monthlyBudgetMinor">Budget mensuel global ; 0 : pas de suivi.</param>
    /// <returns>Les alertes les plus graves d'abord, puis les échéances par date.</returns>
    public static IReadOnlyList<AppNotification> Build(FinanceData data, DateTime today, long monthlyBudgetMinor)
    {
        today = today.Date;
        var month = DashboardCalculator.Build(data, DashboardPeriod.Month, today, monthlyBudgetMinor);
        var stamp = today.ToString("yyyyMM", CultureInfo.InvariantCulture);
        var alerts = new List<AppNotification>();

        if (month.Financial.ForecastBalanceMinor < 0)
            alerts.Add(new($"overdraft:{stamp}", NotificationTopic.Overdraft, NotificationSeverity.Critical, string.Empty, month.Financial.ForecastBalanceMinor, 0, 0));

        if (month.MonthlyBudget is { Level: not BudgetLevel.Ok } global)
            alerts.Add(new($"budget:{stamp}:{global.Level}", NotificationTopic.MonthlyBudget, Severity(global.Level), string.Empty, global.SpentMinor, global.BudgetMinor, 0));

        foreach (var spend in month.Categories)
        {
            if (spend.Budget is not { Level: not BudgetLevel.Ok } budget) continue;
            alerts.Add(new($"category:{spend.Name}:{stamp}:{budget.Level}", NotificationTopic.CategoryBudget, Severity(budget.Level), spend.Name, budget.SpentMinor, budget.BudgetMinor, 0));
        }

        var last = today.AddDays(UpcomingDays);
        foreach (var subscription in data.Subscriptions
                     .Where(x => x.DeletedAtUtc is null && x.NextBillingDate.Date >= today && x.NextBillingDate.Date <= last)
                     .OrderBy(x => x.NextBillingDate))
        {
            var date = subscription.NextBillingDate.Date;
            alerts.Add(new($"subscription:{subscription.Id}:{date.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}",
                NotificationTopic.SubscriptionDue, NotificationSeverity.Info, subscription.Name, subscription.AmountMinor, 0, (date - today).Days));
        }

        return alerts.OrderByDescending(x => x.Severity).ToList();
    }

    private static NotificationSeverity Severity(BudgetLevel level) =>
        level == BudgetLevel.Exceeded ? NotificationSeverity.Critical : NotificationSeverity.Warning;
}
