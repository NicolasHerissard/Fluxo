using Finance.Android17.Application;
using Finance.Android17.Presentation;
using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Presentation;

public sealed class UpdateViewModelTests
{
    private static readonly AppUpdateInfo Update = new(110, "APK #10", "https://github.com/x/y.apk", 52_428_800, null, "");

    [Fact]
    public async Task Check_WhenUpToDate_SaysSo()
    {
        var service = new FakeAppUpdateService();
        var vm = new UpdateViewModel(service);

        await vm.CheckCommand.ExecuteAsync(null);

        Assert.Equal("Fluxo est à jour.", vm.StatusMessage);
        Assert.False(vm.HasUpdate);
    }

    [Fact]
    public async Task Check_WhenAvailable_ShowsTheVersionButInstallsNothing()
    {
        var service = new FakeAppUpdateService { Available = Update };
        var vm = new UpdateViewModel(service);

        await vm.CheckCommand.ExecuteAsync(null);

        Assert.True(vm.HasUpdate);
        Assert.Contains("APK #10", vm.AvailableText);
        Assert.Contains("50 Mo", vm.AvailableText);
        Assert.Empty(service.Installed);
    }

    [Fact]
    public async Task Check_Failure_ShowsAClearError()
    {
        var vm = new UpdateViewModel(new FakeAppUpdateService { Failure = new InvalidOperationException("Impossible de joindre le serveur.") });

        await vm.CheckCommand.ExecuteAsync(null);

        Assert.True(vm.StatusIsError);
        Assert.Equal("Erreur : Impossible de joindre le serveur.", vm.StatusText);
        Assert.False(vm.HasUpdate);
    }

    [Fact]
    public async Task Install_RequiresTheUsersConfirmation()
    {
        var service = new FakeAppUpdateService { Available = Update };
        var vm = new UpdateViewModel(service);
        await vm.CheckCommand.ExecuteAsync(null);

        await vm.InstallCommand.ExecuteAsync(null); // aucune confirmation branchée
        vm.ConfirmInstallAsync = (_, _, _) => Task.FromResult(false);
        await vm.InstallCommand.ExecuteAsync(null); // refusée

        Assert.Empty(service.Installed);
    }

    [Fact]
    public async Task Install_AfterConfirmation_HandsOverToTheInstaller()
    {
        var service = new FakeAppUpdateService { Available = Update };
        var vm = new UpdateViewModel(service) { ConfirmInstallAsync = (_, _, _) => Task.FromResult(true) };
        await vm.CheckCommand.ExecuteAsync(null);

        await vm.InstallCommand.ExecuteAsync(null);

        Assert.Equal([Update], service.Installed);
        Assert.False(vm.StatusIsError);
    }

    [Fact]
    public async Task Install_Failure_IsReportedWithoutCrashing()
    {
        var service = new FakeAppUpdateService { Available = Update };
        var vm = new UpdateViewModel(service) { ConfirmInstallAsync = (_, _, _) => Task.FromResult(true) };
        await vm.CheckCommand.ExecuteAsync(null);
        service.Failure = new IOException("disque plein");

        await vm.InstallCommand.ExecuteAsync(null);

        Assert.True(vm.StatusIsError);
        Assert.Contains("La mise à jour a échoué", vm.StatusMessage);
        Assert.False(vm.IsBusy);
    }
}
