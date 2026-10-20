namespace Finance.Android17.Domain;

public sealed class Expense : Entity
{
    public long AmountMinor { get; set; }
    public string CurrencyCode { get; set; } = "EUR";
    public string Description { get; set; } = string.Empty;
    public DateTime OccurredOn { get; set; } = DateTime.Today;
    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }

    public decimal Amount => AmountMinor / 100m;
}
