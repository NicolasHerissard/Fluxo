using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Persistence;

public sealed class IncomeRepositoryTests : IDisposable
{
    private readonly InfrastructureHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task Incomes_CanBeAddedUpdatedAndDeleted()
    {
        await _host.Incomes.AddAsync(250_000, " Salaire ", new DateTime(2026, 10, 1, 8, 0, 0));
        var income = Assert.Single(await _host.Incomes.GetAllAsync());
        Assert.Equal("Salaire", income.Description);
        Assert.Equal(new DateTime(2026, 10, 1), income.OccurredOn);

        await _host.Incomes.UpdateAsync(income.Id, 260_000, "Prime", new DateTime(2026, 10, 2));
        var updated = Assert.Single(await _host.Incomes.GetAllAsync());
        Assert.Equal((260_000, "Prime"), (updated.AmountMinor, updated.Description));
        Assert.NotEqual(income.Version, updated.Version);

        await _host.Incomes.DeleteAsync(income.Id);
        Assert.Empty(await _host.Incomes.GetAllAsync());
    }

    [Fact]
    public async Task Incomes_AreSortedMostRecentFirst()
    {
        await _host.Incomes.AddAsync(1, "ancien", new DateTime(2026, 9, 1));
        await _host.Incomes.AddAsync(2, "récent", new DateTime(2026, 10, 1));

        Assert.Equal(["récent", "ancien"], (await _host.Incomes.GetAllAsync()).Select(x => x.Description));
    }

    [Fact]
    public async Task RecurringIncomes_AreSortedByNextOccurrence()
    {
        await _host.RecurringIncomes.AddAsync("Loyer", 50_000, 1, new DateTime(2026, 12, 1));
        await _host.RecurringIncomes.AddAsync("Prime", 10_000, 12, new DateTime(2026, 11, 1));

        Assert.Equal(["Prime", "Loyer"], (await _host.RecurringIncomes.GetAllAsync()).Select(x => x.Name));
    }

    [Fact]
    public async Task RecurringIncome_IntervalIsClampedToAtLeastOneMonth()
    {
        await _host.RecurringIncomes.AddAsync("Loyer perçu", 50_000, 0, new DateTime(2026, 11, 1));
        var added = Assert.Single(await _host.RecurringIncomes.GetAllAsync());
        Assert.Equal(1, added.IntervalMonths);

        await _host.RecurringIncomes.UpdateAsync(added.Id, "Loyer perçu", 50_000, -3, new DateTime(2026, 11, 1));
        Assert.Equal(1, Assert.Single(await _host.RecurringIncomes.GetAllAsync()).IntervalMonths);
    }

    [Fact]
    public async Task RecurringIncome_CanBeUpdatedAndDeleted()
    {
        await _host.RecurringIncomes.AddAsync(" Loyer ", 50_000, 3, new DateTime(2026, 11, 1, 10, 0, 0), "CHF");
        var added = Assert.Single(await _host.RecurringIncomes.GetAllAsync());
        Assert.Equal(("Loyer", 3, new DateTime(2026, 11, 1), "CHF"), (added.Name, added.IntervalMonths, added.NextOccurrenceDate, added.CurrencyCode));

        await _host.RecurringIncomes.UpdateAsync(added.Id, "Loyer 2", 60_000, 12, new DateTime(2027, 1, 1));
        var updated = Assert.Single(await _host.RecurringIncomes.GetAllAsync());
        Assert.Equal(("Loyer 2", 60_000L, 12), (updated.Name, updated.AmountMinor, updated.IntervalMonths));

        await _host.RecurringIncomes.DeleteAsync(added.Id);
        Assert.Empty(await _host.RecurringIncomes.GetAllAsync());
    }
}
