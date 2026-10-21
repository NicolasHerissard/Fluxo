namespace Finance.Android17.Application;

public enum BudgetLevel { Ok, Warning, Exceeded }

/// <summary>Consommation d'un budget (global ou de catégorie). Alerte à 80 % puis à 100 %.</summary>
public sealed record BudgetStatus(long BudgetMinor, long SpentMinor)
{
    public const double WarningThreshold = 0.8;

    public long RemainingMinor => BudgetMinor - SpentMinor;

    public double Progress => BudgetMinor <= 0 ? 0 : Math.Clamp((double)SpentMinor / BudgetMinor, 0, 1);

    public BudgetLevel Level => Progress >= 1 ? BudgetLevel.Exceeded : Progress >= WarningThreshold ? BudgetLevel.Warning : BudgetLevel.Ok;
}
