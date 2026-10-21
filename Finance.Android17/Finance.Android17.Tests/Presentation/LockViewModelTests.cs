using Finance.Android17.Domain;
using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Presentation;

public sealed class LockViewModelTests : IDisposable
{
    private readonly AppFixture _app = new();

    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task Initialize_WithoutPin_AsksToCreateOne()
    {
        var vm = _app.CreateLock(new FakeAppLock());

        await vm.InitializeAsync();

        Assert.True(vm.IsFirstRun);
        Assert.Equal("Créer votre code PIN", vm.PinTitle);
        Assert.False(vm.CanUseBiometrics);
    }

    [Fact]
    public async Task Initialize_WithPin_OffersBiometrics_WhenEnabledAndAvailable()
    {
        _app.Settings.BiometricsEnabled = true;
        var vm = _app.CreateLock(new FakeAppLock("1234"), new FakeBiometrics(isAvailable: true));

        await vm.InitializeAsync();

        Assert.False(vm.IsFirstRun);
        Assert.Equal("Bon retour", vm.PinTitle);
        Assert.True(vm.CanUseBiometrics);
    }

    [Fact]
    public void PinDots_FollowTheTypedPin()
    {
        var vm = _app.CreateLock(new FakeAppLock("1234"));
        Assert.Equal("○ ○ ○ ○", vm.PinDots);

        vm.Pin = "12";
        Assert.Equal("● ● ○ ○", vm.PinDots);

        vm.Pin = "123456";
        Assert.Equal("● ● ● ● ● ●", vm.PinDots);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("123456789")]
    [InlineData("12a4")]
    [InlineData("")]
    public async Task SubmitPin_RejectsMalformedPins(string pin)
    {
        var appLock = new FakeAppLock();
        var vm = _app.CreateLock(appLock);
        await vm.InitializeAsync();
        var unlocked = false;
        vm.Unlocked += (_, _) => unlocked = true;

        vm.Pin = pin;
        await vm.SubmitPinCommand.ExecuteAsync(null);

        Assert.Equal("Le PIN doit contenir entre 4 et 8 chiffres.", vm.ErrorMessage);
        Assert.False(unlocked);
        Assert.Null(appLock.Pin);
    }

    [Fact]
    public async Task SubmitPin_FirstRun_CreatesThePin_AndUnlocks()
    {
        var appLock = new FakeAppLock();
        var vm = _app.CreateLock(appLock);
        await vm.InitializeAsync();
        var unlocked = false;
        vm.Unlocked += (_, _) => unlocked = true;

        vm.Pin = "4821";
        await vm.SubmitPinCommand.ExecuteAsync(null);

        Assert.Equal("4821", appLock.Pin);
        Assert.True(unlocked);
        Assert.Equal(string.Empty, vm.Pin);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task SubmitPin_WrongPin_ClearsItAndStaysLocked()
    {
        var vm = _app.CreateLock(new FakeAppLock("1234"));
        await vm.InitializeAsync();
        var unlocked = false;
        vm.Unlocked += (_, _) => unlocked = true;

        vm.Pin = "9999";
        await vm.SubmitPinCommand.ExecuteAsync(null);

        Assert.Equal("Code PIN incorrect. Réessayez.", vm.ErrorMessage);
        Assert.Equal(string.Empty, vm.Pin);
        Assert.False(unlocked);
    }

    [Fact]
    public async Task Unlock_CreatesDueSubscriptionExpensesAndRecurringIncomes_WhenEnabled()
    {
        _app.Settings.AutoCreateSubscriptionExpenses = true;
        await _app.Subscriptions.AddAsync("Netflix", 1_599, 1, new DateTime(2026, 9, 5));
        await _app.RecurringIncomes.AddAsync("Salaire", 200_000, 1, new DateTime(2026, 10, 1));
        var vm = _app.CreateLock(new FakeAppLock("1234"));
        await vm.InitializeAsync();

        vm.Pin = "1234";
        await vm.SubmitPinCommand.ExecuteAsync(null);

        Assert.Equal(["Abonnement · Netflix", "Abonnement · Netflix"], (await _app.Expenses.GetAllAsync()).Select(x => x.Description));
        Assert.Equal(new DateTime(2026, 11, 5), Assert.Single(await _app.Subscriptions.GetAllAsync()).NextBillingDate);
        Assert.Equal(200_000, Assert.Single(await _app.Incomes.GetAllAsync()).AmountMinor);
        var plan = Assert.Single(_app.Reminders.Plans);
        Assert.Equal(new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero), Assert.Single(plan.Alarms).TriggerAt);
    }

    [Fact]
    public async Task Unlock_LeavesDueOperationsAlone_WhenAutoCreationIsDisabled()
    {
        _app.Settings.AutoCreateSubscriptionExpenses = false;
        _app.Settings.AutoCreateRecurringIncomes = false;
        _app.Settings.SubscriptionRemindersEnabled = false;
        await _app.Subscriptions.AddAsync("Netflix", 1_599, 1, new DateTime(2026, 9, 5));
        await _app.RecurringIncomes.AddAsync("Salaire", 200_000, 1, new DateTime(2026, 10, 1));
        var vm = _app.CreateLock(new FakeAppLock("1234"));
        await vm.InitializeAsync();

        vm.Pin = "1234";
        await vm.SubmitPinCommand.ExecuteAsync(null);

        Assert.Empty(await _app.Expenses.GetAllAsync());
        Assert.Empty(await _app.Incomes.GetAllAsync());
        // Rappels désactivés : seul un plan vide est appliqué, pour annuler d'éventuelles alarmes restantes.
        var plan = Assert.Single(_app.Reminders.Plans);
        Assert.Empty(plan.Alarms);
    }

    [Theory]
    [InlineData(30, "Trop d’essais. Réessayez dans 30 s.")]
    [InlineData(90, "Trop d’essais. Réessayez dans 2 min.")]
    public async Task WrongPin_WhileLockedOut_ShowsTheWait_AndDoesNotUnlock(int seconds, string expected)
    {
        var appLock = new FakeAppLock("1234") { LockoutRemaining = TimeSpan.FromSeconds(seconds) };
        var vm = _app.CreateLock(appLock);
        await vm.InitializeAsync();
        var unlocked = false;
        vm.Unlocked += (_, _) => unlocked = true;

        vm.Pin = "1234";
        await vm.SubmitPinCommand.ExecuteAsync(null);

        Assert.Equal(expected, vm.ErrorMessage);
        Assert.False(unlocked);
    }

    [Fact]
    public async Task Biometrics_Success_Unlocks_AndFailureDoesNot()
    {
        _app.Settings.BiometricsEnabled = true;
        var accepted = _app.CreateLock(new FakeAppLock("1234"), new FakeBiometrics(isAvailable: true, succeeds: true));
        await accepted.InitializeAsync();
        var acceptedUnlocked = false;
        accepted.Unlocked += (_, _) => acceptedUnlocked = true;
        await accepted.UseBiometricsCommand.ExecuteAsync(null);
        Assert.True(acceptedUnlocked);

        var refused = _app.CreateLock(new FakeAppLock("1234"), new FakeBiometrics(isAvailable: true, succeeds: false));
        await refused.InitializeAsync();
        var refusedUnlocked = false;
        refused.Unlocked += (_, _) => refusedUnlocked = true;
        await refused.UseBiometricsCommand.ExecuteAsync(null);
        Assert.False(refusedUnlocked);
    }

    [Fact]
    public async Task TogglePinVisibility_SwitchesTheIcon()
    {
        var vm = _app.CreateLock(new FakeAppLock("1234"));
        await vm.InitializeAsync();

        vm.TogglePinVisibilityCommand.Execute(null);

        Assert.False(vm.IsPinHidden);
        Assert.Equal("icon_visibility_off.png", vm.PinVisibilityIcon);
    }
}
