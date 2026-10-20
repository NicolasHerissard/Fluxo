using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Persistence;

public sealed class FinancialProfileRepositoryTests : IDisposable
{
    private readonly InfrastructureHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task InitialBalance_DefaultsToZero_AndCanBeNegative()
    {
        Assert.Equal(0, await _host.Profile.GetInitialBalanceAsync());

        await _host.Profile.SetInitialBalanceAsync(-12_345);

        Assert.Equal(-12_345, await _host.Profile.GetInitialBalanceAsync());
    }

    [Fact]
    public async Task InitialBalance_SurvivesRestart()
    {
        await _host.Profile.SetInitialBalanceAsync(98_765);

        using var restarted = new InfrastructureHost(databaseOf: _host, secrets: _host.Secrets);

        Assert.Equal(98_765, await restarted.Profile.GetInitialBalanceAsync());
    }
}
