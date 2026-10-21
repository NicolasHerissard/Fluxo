namespace Finance.Android17.Application;

public enum ExpenseSort { Newest, Oldest, AmountDescending, AmountAscending, NameAscending, NameDescending }

public enum ExpensePeriod { AllTime, Today, Last7Days, ThisMonth, ThisYear, Custom }

/// <summary>Filtres et tri de la liste des dépenses. Un critère absent ou vide n'exclut rien.</summary>
public sealed record ExpenseCriteria(
    string? SearchText = null,
    IReadOnlySet<Guid>? CategoryIds = null,
    DateTime? From = null,
    DateTime? To = null,
    long? MinimumMinor = null,
    long? MaximumMinor = null,
    ExpenseSort Sort = ExpenseSort.Newest);

public static class ExpensePeriods
{
    /// <summary>Bornes (incluses) d'une période relative à aujourd'hui ; <see langword="null"/> si la borne est ouverte.</summary>
    public static (DateTime? From, DateTime? To) Resolve(ExpensePeriod period, DateTime today, DateTime customFrom, DateTime customTo)
    {
        today = today.Date;
        return period switch
        {
            ExpensePeriod.Today => (today, today),
            ExpensePeriod.Last7Days => (today.AddDays(-6), today),
            ExpensePeriod.ThisMonth => (new DateTime(today.Year, today.Month, 1), today),
            ExpensePeriod.ThisYear => (new DateTime(today.Year, 1, 1), today),
            ExpensePeriod.Custom => (customFrom.Date, customTo.Date),
            _ => (null, null)
        };
    }
}
