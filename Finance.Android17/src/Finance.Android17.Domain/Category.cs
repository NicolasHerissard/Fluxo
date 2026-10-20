namespace Finance.Android17.Domain;

public sealed class Category : Entity
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#6750A4";
    public long? BudgetMinor { get; set; }
}
