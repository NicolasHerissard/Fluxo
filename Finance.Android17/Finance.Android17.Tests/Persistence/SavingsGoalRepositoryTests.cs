using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Persistence;

public sealed class SavingsGoalRepositoryTests : IDisposable
{
    private readonly InfrastructureHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task Goals_AreSortedByTargetDateThenName_AndCanBeUpdatedAndDeleted()
    {
        await _host.SavingsGoals.AddAsync("Vacances", 200_000, 50_000, new DateTime(2027, 6, 1, 10, 0, 0));
        await _host.SavingsGoals.AddAsync("Voiture", 1_000_000, 0, new DateTime(2027, 1, 1));
        await _host.SavingsGoals.AddAsync("Coussin", 300_000, 0, null);

        var goals = await _host.SavingsGoals.GetAllAsync();
        Assert.Equal(["Coussin", "Voiture", "Vacances"], goals.Select(x => x.Name));
        Assert.Equal(new DateTime(2027, 6, 1), goals.Single(x => x.Name == "Vacances").TargetDate);

        var vacation = goals.Single(x => x.Name == "Vacances");
        await _host.SavingsGoals.UpdateAsync(vacation.Id, "Voyage", 250_000, 100_000, null);
        var updated = (await _host.SavingsGoals.GetAllAsync()).Single(x => x.Name == "Voyage");
        Assert.Equal(0.4, updated.Progress, 5);
        Assert.NotEqual(vacation.Version, updated.Version);

        await _host.SavingsGoals.DeleteAsync(vacation.Id);
        Assert.DoesNotContain(await _host.SavingsGoals.GetAllAsync(), x => x.Id == vacation.Id);
    }

    [Fact]
    public async Task Add_TrimsTheName()
    {
        await _host.SavingsGoals.AddAsync("  Vélo ", 100_000, 0, null);

        Assert.Equal("Vélo", Assert.Single(await _host.SavingsGoals.GetAllAsync()).Name);
    }
}
