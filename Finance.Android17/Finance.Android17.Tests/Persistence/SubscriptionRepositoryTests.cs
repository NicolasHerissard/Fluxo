using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Persistence;

public sealed class SubscriptionRepositoryTests : IDisposable
{
    private readonly InfrastructureHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task Add_TrimsTheName_DropsTheTime_AndKeepsTheCurrency()
    {
        await _host.Subscriptions.AddAsync("  Netflix ", 1299, 1, new DateTime(2026, 11, 15, 14, 0, 0), "USD");

        var subscription = Assert.Single(await _host.Subscriptions.GetAllAsync());
        Assert.Equal(("Netflix", 1299L, "USD"), (subscription.Name, subscription.AmountMinor, subscription.CurrencyCode));
        Assert.Equal(new DateTime(2026, 11, 15), subscription.NextBillingDate);
    }

    [Fact]
    public async Task GetAll_IsSortedByNextBillingDate()
    {
        await _host.Subscriptions.AddAsync("Plus tard", 1, 1, new DateTime(2026, 12, 1));
        await _host.Subscriptions.AddAsync("Bientôt", 1, 1, new DateTime(2026, 11, 1));

        Assert.Equal(["Bientôt", "Plus tard"], (await _host.Subscriptions.GetAllAsync()).Select(x => x.Name));
    }

    [Fact]
    public async Task Interval_IsClampedToAtLeastOneMonth_WhenAddingAndUpdating()
    {
        await _host.Subscriptions.AddAsync("Zéro", 1200, 0, new DateTime(2026, 11, 1));
        var added = Assert.Single(await _host.Subscriptions.GetAllAsync());
        Assert.Equal(1, added.BillingIntervalMonths);

        await _host.Subscriptions.UpdateAsync(added.Id, "Zéro", 1200, -4, new DateTime(2026, 11, 1));
        Assert.Equal(1, Assert.Single(await _host.Subscriptions.GetAllAsync()).BillingIntervalMonths);
    }

    [Fact]
    public async Task Update_ChangesTheFields_AndRenewsTheVersion()
    {
        await _host.Subscriptions.AddAsync("Netflix", 1299, 1, new DateTime(2026, 11, 15));
        var before = Assert.Single(await _host.Subscriptions.GetAllAsync());

        await _host.Subscriptions.UpdateAsync(before.Id, "Netflix 4K", 1799, 12, new DateTime(2027, 1, 1), "CHF");

        var after = Assert.Single(await _host.Subscriptions.GetAllAsync());
        Assert.Equal(("Netflix 4K", 1799L, 12, "CHF"), (after.Name, after.AmountMinor, after.BillingIntervalMonths, after.CurrencyCode));
        Assert.Equal(new DateTime(2027, 1, 1), after.NextBillingDate);
        Assert.NotEqual(before.Version, after.Version);
    }

    [Fact]
    public async Task Delete_HidesTheSubscription_AndRenewsItsVersion()
    {
        await _host.Subscriptions.AddAsync("Netflix", 1299, 1, new DateTime(2026, 11, 15));
        var id = Assert.Single(await _host.Subscriptions.GetAllAsync()).Id;
        var before = await _host.ScalarAsync<string>("SELECT Version FROM Subscriptions");

        await _host.Subscriptions.DeleteAsync(id);

        Assert.Empty(await _host.Subscriptions.GetAllAsync());
        Assert.NotEqual(before, await _host.ScalarAsync<string>("SELECT Version FROM Subscriptions"));
    }

    [Fact]
    public async Task Restore_BringsBackADeletedSubscription()
    {
        await _host.Subscriptions.AddAsync("Netflix", 1299, 1, new DateTime(2026, 11, 15));
        var id = Assert.Single(await _host.Subscriptions.GetAllAsync()).Id;
        await _host.Subscriptions.DeleteAsync(id);

        await _host.Subscriptions.RestoreAsync(id);

        Assert.Equal(id, Assert.Single(await _host.Subscriptions.GetAllAsync()).Id);
    }
}
