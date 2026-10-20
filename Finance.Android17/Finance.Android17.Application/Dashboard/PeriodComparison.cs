namespace Finance.Android17.Application;

public enum TrendDirection { NoReference, Down, Up, Same }

/// <summary>Évolution des dépenses d'une période par rapport à la précédente.</summary>
public sealed record PeriodComparison(TrendDirection Direction, double Percent)
{
    public static PeriodComparison Between(long current, long previous)
    {
        if (previous == 0) return new PeriodComparison(TrendDirection.NoReference, 0);
        var difference = current - previous;
        var percent = Math.Abs(difference * 100d / previous);
        return new PeriodComparison(difference < 0 ? TrendDirection.Down : difference > 0 ? TrendDirection.Up : TrendDirection.Same, percent);
    }
}
