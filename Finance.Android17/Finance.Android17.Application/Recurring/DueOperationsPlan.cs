using Finance.Android17.Domain;

namespace Finance.Android17.Application;

/// <summary>Nouvelle prochaine échéance d'un abonnement ou d'un revenu récurrent déjà traité.</summary>
public sealed record DueAdvance(Guid Id, DateTime NextDate);

/// <summary>
/// Ce qu'il faut enregistrer pour rattraper les échéances manquées. Les dépenses n'ont pas encore de catégorie :
/// le magasin les rattache à la catégorie des abonnements (<see cref="DefaultCategories.SubscriptionsName"/>), créée au besoin.
/// </summary>
public sealed record DueOperationsPlan(
    IReadOnlyList<Expense> Expenses,
    IReadOnlyList<DueAdvance> SubscriptionAdvances,
    IReadOnlyList<Income> Incomes,
    IReadOnlyList<DueAdvance> RecurringIncomeAdvances)
{
    public static DueOperationsPlan Empty { get; } = new([], [], [], []);

    public bool IsEmpty => Expenses.Count == 0 && Incomes.Count == 0 && SubscriptionAdvances.Count == 0 && RecurringIncomeAdvances.Count == 0;
}

public sealed record DueOperationsResult(int ExpensesCreated, int IncomesCreated);
