namespace Finance.Android17.Domain;

public sealed class SavingsGoal : Entity
{
    public string Name { get; set; } = string.Empty;
    public long TargetAmountMinor { get; set; }
    public long SavedAmountMinor { get; set; }
    public DateTime? TargetDate { get; set; }

    public double Progress => TargetAmountMinor <= 0
        ? 0
        : Math.Clamp((double)SavedAmountMinor / TargetAmountMinor, 0, 1);
}
