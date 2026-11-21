using Finance.Android17.Application;
using Finance.Android17.Infrastructure;
using Finance.Android17.Presentation;
using Finance.Android17.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Android17.Tests.Presentation;

/// <summary>La composition de l'application (<c>AddInfrastructure</c>, <c>AddApplication</c>, <c>AddPresentation</c>) se résout entièrement.</summary>
public sealed class CompositionTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly ServiceProvider _provider;

    public CompositionTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISecureValueStore>(new FakeSecureValueStore());
        services.AddSingleton<IFileExchange>(new FakeFileExchange());
        services.AddSingleton<ISettingsService>(new FakeSettings());
        services.AddSingleton<IReminderService>(new FakeReminderService());
        services.AddSingleton<IAlertPublisher>(new FakeAlertPublisher());
        services.AddSingleton<IDailyCheckScheduler>(new FakeDailyCheckScheduler());
        services.AddSingleton<IBiometricService>(new FakeBiometrics());
        services.AddSingleton<IOcrService>(new FakeOcrService());
        services.AddSingleton<IAppUpdateService>(new FakeAppUpdateService());
        services.AddInfrastructure(_db.Options).AddApplication().AddPresentation();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    public void Dispose()
    {
        _provider.Dispose();
        _db.Dispose();
    }

    [Fact]
    public void EveryViewModel_IsResolvable()
    {
        Assert.NotNull(_provider.GetRequiredService<LockViewModel>());
        Assert.NotNull(_provider.GetRequiredService<DashboardViewModel>());
        Assert.NotNull(_provider.GetRequiredService<ExpensesViewModel>());
        Assert.NotNull(_provider.GetRequiredService<IncomeViewModel>());
        Assert.NotNull(_provider.GetRequiredService<SubscriptionsViewModel>());
        Assert.NotNull(_provider.GetRequiredService<SettingsViewModel>());
        Assert.NotNull(_provider.GetRequiredService<CategoriesSettingsViewModel>());
        Assert.NotNull(_provider.GetRequiredService<DataSettingsViewModel>());
        Assert.NotNull(_provider.GetRequiredService<AddExpenseViewModel>());
        Assert.NotNull(_provider.GetRequiredService<NotificationsViewModel>());
        Assert.NotNull(_provider.GetRequiredService<UpdateViewModel>());
    }

    [Fact]
    public void ViewModels_AreTransient_ServicesAreSingletons()
    {
        Assert.NotSame(_provider.GetRequiredService<DashboardViewModel>(), _provider.GetRequiredService<DashboardViewModel>());
        Assert.Same(_provider.GetRequiredService<IExpenseRepository>(), _provider.GetRequiredService<IExpenseRepository>());
        Assert.Same(_provider.GetRequiredService<IDatabaseInitializer>(), _provider.GetRequiredService<IDatabaseInitializer>());
        Assert.Same(_provider.GetRequiredService<StartupService>(), _provider.GetRequiredService<StartupService>());
    }

    [Fact]
    public void EveryPort_HasAnImplementation()
    {
        foreach (var port in new[]
        {
            typeof(ISecretStore), typeof(IAppLockService), typeof(IDatabaseInitializer), typeof(IExpenseRepository),
            typeof(ICategoryRepository), typeof(IIncomeRepository), typeof(IRecurringIncomeRepository),
            typeof(ISubscriptionRepository), typeof(ISavingsGoalRepository), typeof(IFinancialProfileRepository),
            typeof(IDueOperationsStore), typeof(IFinanceDataStore), typeof(IExportService), typeof(IDataBackupService)
        })
            Assert.NotNull(_provider.GetRequiredService(port));
    }

    [Fact]
    public async Task ADashboard_CanBeLoaded_FromTheRealComposition()
    {
        var dashboard = _provider.GetRequiredService<DashboardViewModel>();

        await dashboard.LoadAsync();

        Assert.False(dashboard.HasData);
        Assert.True(dashboard.IsEmpty);
    }
}
