using Finance.Android17.Domain;

namespace Finance.Android17.Application;

public enum OperationKind { Income, Expense }

/// <summary>Une ligne du journal des opérations ; un revenu est positif, une dépense négative.</summary>
public sealed record Operation(OperationKind Kind, string Description, DateTime OccurredOn, long SignedAmountMinor);

public static class OperationsFeed
{
    public const int DefaultCount = 20;

    /// <summary>Revenus et dépenses mêlés, du plus récent au plus ancien.</summary>
    public static IReadOnlyList<Operation> Recent(IEnumerable<Expense> expenses, IEnumerable<Income> incomes, int count = DefaultCount) =>
        incomes.Select(x => (x.OccurredOn, x.CreatedAtUtc, Operation: new Operation(OperationKind.Income, x.Description, x.OccurredOn, x.AmountMinor)))
            .Concat(expenses.Select(x => (x.OccurredOn, x.CreatedAtUtc, Operation: new Operation(OperationKind.Expense, x.Description, x.OccurredOn, -x.AmountMinor))))
            .OrderByDescending(x => x.OccurredOn).ThenByDescending(x => x.CreatedAtUtc)
            .Take(count)
            .Select(x => x.Operation)
            .ToList();
}
