using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Presentation;

public sealed class DataSettingsViewModelTests : IDisposable
{
    private readonly AppFixture _app = new();

    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task Load_ShowsTheStoredInitialBalance()
    {
        await _app.Profile.SetInitialBalanceAsync(125_000);
        var vm = _app.CreateData();

        await vm.LoadAsync();

        Assert.Equal("1250", vm.InitialBalance);
    }

    [Theory]
    [InlineData("1000,50", 100_050, "1000,5")]
    [InlineData("-50", -5_000, "-50")]
    [InlineData("0", 0, "0")]
    public async Task SaveInitialBalance_AcceptsSignedAmounts(string input, long expectedMinor, string expectedText)
    {
        var vm = _app.CreateData();

        vm.InitialBalance = input;
        await vm.SaveInitialBalanceCommand.ExecuteAsync(null);

        Assert.Equal(expectedMinor, await _app.Profile.GetInitialBalanceAsync());
        Assert.Equal("Solde initial enregistré.", vm.StatusMessage);
        Assert.Equal(expectedText, vm.InitialBalance);
    }

    [Fact]
    public async Task SaveInitialBalance_RejectsText()
    {
        var vm = _app.CreateData();

        vm.InitialBalance = "beaucoup";
        await vm.SaveInitialBalanceCommand.ExecuteAsync(null);

        Assert.Equal("Indiquez un solde initial valide.", vm.StatusMessage);
        Assert.Equal(0, await _app.Profile.GetInitialBalanceAsync());
    }
}
