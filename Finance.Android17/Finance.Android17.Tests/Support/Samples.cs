using Finance.Android17.Domain;

namespace Finance.Android17.Tests.Support;

/// <summary>Entités du domaine prêtes à l'emploi, sans passer par la base : pour tester les calculs purs.</summary>
public static class Samples
{
    public static Category Category(string name, string color = "#112233", long? budgetMinor = null) =>
        new() { Name = name, Color = color, BudgetMinor = budgetMinor };

    public static Expense Expense(long amountMinor, DateTime date, Category? category = null, string description = "", DateTime? createdAtUtc = null) =>
        new()
        {
            AmountMinor = amountMinor,
            OccurredOn = date,
            Description = description,
            Category = category,
            CategoryId = category?.Id ?? Guid.Empty,
            CreatedAtUtc = createdAtUtc ?? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

    public static Income Income(long amountMinor, DateTime date, string description = "", DateTime? createdAtUtc = null) =>
        new()
        {
            AmountMinor = amountMinor,
            OccurredOn = date,
            Description = description,
            CreatedAtUtc = createdAtUtc ?? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

    public static Subscription Subscription(long amountMinor, DateTime nextBilling, int intervalMonths = 1, string name = "") =>
        new() { Name = name, AmountMinor = amountMinor, NextBillingDate = nextBilling, BillingIntervalMonths = intervalMonths };

    public static RecurringIncome RecurringIncome(long amountMinor, DateTime nextOccurrence, int intervalMonths = 1) =>
        new() { AmountMinor = amountMinor, NextOccurrenceDate = nextOccurrence, IntervalMonths = intervalMonths };
}
