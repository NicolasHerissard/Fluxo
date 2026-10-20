namespace Finance.Android17.Domain;

public sealed class FinancialProfile
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public long InitialBalanceMinor { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
