namespace Finance.Android17.Domain;

public sealed class RecurringIncome : Entity
{
    public string Name { get; set; } = string.Empty;
    public long AmountMinor { get; set; }
    public string CurrencyCode { get; set; } = "EUR";
    public int IntervalMonths { get; set; } = 1;
    public DateTime NextOccurrenceDate { get; set; } = DateTime.Today;

    public decimal Amount => AmountMinor / 100m;
    public long MonthlyEquivalentMinor => IntervalMonths <= 0 ? AmountMinor : AmountMinor / IntervalMonths;
}
