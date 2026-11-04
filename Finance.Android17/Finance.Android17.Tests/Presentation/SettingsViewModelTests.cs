using Finance.Android17.Presentation;
using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Presentation;

public sealed class SettingsViewModelTests : IDisposable
{
    private readonly AppFixture _app = new();

    public void Dispose() => _app.Dispose();

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
