using Finance.Android17.Presentation;
using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Presentation;

public sealed class SettingsViewModelTests : IDisposable
{
    private readonly AppFixture _app = new();

    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task Load_ShowsTheStoredInitialBalance()
    {
        await _app.Profile.SetInitialBalanceAsync(125_000);
        var vm = _app.CreateSettings();

        await vm.LoadAsync();

        Assert.Equal("1250", vm.InitialBalance);
    }

    [Theory]
    [InlineData("1000,50", 100_050, "1000,5")]
    [InlineData("-50", -5_000, "-50")]
    [InlineData("0", 0, "0")]
    public async Task SaveInitialBalance_AcceptsSignedAmounts(string input, long expectedMinor, string expectedText)
    {
        var vm = _app.CreateSettings();

        vm.InitialBalance = input;
        await vm.SaveInitialBalanceCommand.ExecuteAsync(null);

        Assert.Equal(expectedMinor, await _app.Profile.GetInitialBalanceAsync());
        Assert.Equal("Solde initial enregistré.", vm.StatusMessage);
        Assert.Equal(expectedText, vm.InitialBalance);
    }

    [Fact]
    public async Task SaveInitialBalance_RejectsText()
    {
        var vm = _app.CreateSettings();

        vm.InitialBalance = "beaucoup";
        await vm.SaveInitialBalanceCommand.ExecuteAsync(null);

        Assert.Equal("Indiquez un solde initial valide.", vm.StatusMessage);
        Assert.Equal(0, await _app.Profile.GetInitialBalanceAsync());
    }

    [Fact]
    public async Task Load_ShowsBackgroundAlerts_AsEnabledByDefault_WithoutSchedulingAnything()
    {
        var vm = _app.CreateSettings();

        await vm.LoadAsync();

        Assert.True(vm.BackgroundAlertsEnabled);
        Assert.Equal(0, _app.DailyChecks.Calls);
    }

    [Fact]
    public async Task TurningBackgroundAlertsOn_SavesTheChoice_AndAsksThePlatformToSchedule()
    {
        _app.Settings.BackgroundAlertsEnabled = false;
        var vm = _app.CreateSettings();
        await vm.LoadAsync();
        Assert.False(vm.BackgroundAlertsEnabled);

        vm.BackgroundAlertsEnabled = true;

        Assert.True(_app.Settings.BackgroundAlertsEnabled);
        Assert.Equal(1, _app.DailyChecks.Calls);
    }

    [Fact]
    public async Task TurningBackgroundAlertsOff_SavesTheChoice_WithoutSchedulingAgain()
    {
        var vm = _app.CreateSettings();
        await vm.LoadAsync();

        vm.BackgroundAlertsEnabled = false;

        Assert.False(_app.Settings.BackgroundAlertsEnabled);
        Assert.Equal(0, _app.DailyChecks.Calls);
    }
}
