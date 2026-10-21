namespace Finance.Android17.Domain;

public sealed class Subscription : Entity
{
    public string Name { get; set; } = string.Empty;
    public long AmountMinor { get; set; }
    public string CurrencyCode { get; set; } = "EUR";
    public int BillingIntervalMonths { get; set; } = 1;
    public DateTime NextBillingDate { get; set; } = DateTime.Today;
    /// <summary>Jour du mois voulu pour l'échéance (1 à 31) ; 0 : celui de <see cref="NextBillingDate"/>. Évite la dérive du 31 vers le 28.</summary>
    public int AnchorDay { get; set; }
    public bool ReminderEnabled { get; set; } = true;

    public decimal Amount => AmountMinor / 100m;
    public long MonthlyEquivalentMinor => BillingIntervalMonths <= 0 ? AmountMinor : AmountMinor / BillingIntervalMonths;
}
