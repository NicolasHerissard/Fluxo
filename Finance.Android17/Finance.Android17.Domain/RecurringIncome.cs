namespace Finance.Android17.Domain;

public sealed class RecurringIncome : Entity
{
    public string Name { get; set; } = string.Empty;
    public long AmountMinor { get; set; }
    public string CurrencyCode { get; set; } = "EUR";
    public int IntervalMonths { get; set; } = 1;
    public DateTime NextOccurrenceDate { get; set; } = DateTime.Today;
    /// <summary>Jour du mois voulu pour l'échéance (1 à 31) ; 0 : celui de <see cref="NextOccurrenceDate"/>. Évite la dérive du 31 vers le 28.</summary>
    public int AnchorDay { get; set; }

    public decimal Amount => AmountMinor / 100m;
    public long MonthlyEquivalentMinor => IntervalMonths <= 0 ? AmountMinor : AmountMinor / IntervalMonths;
}
