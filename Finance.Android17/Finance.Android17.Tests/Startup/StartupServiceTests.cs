using Finance.Android17.Application;
using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Startup;

public sealed class StartupServiceTests : IDisposable
{
    private readonly AppFixture _app = new();

    public void Dispose() => _app.Dispose();

    private StartupService Startup => _app.Get<StartupService>();

    [Fact]
    public async Task Prepare_OpensTheDatabase_AndSeedsTheDefaultCategories()
    {
        await Startup.PrepareAsync();

        Assert.Equal(["Alimentation", "Autre", "Logement", "Loisirs", "Transport"], (await _app.Categories.GetAllAsync()).Select(x => x.Name));
    }

    [Fact]
    public async Task Prepare_CatchesUpDueOperations_ThenSchedulesRemindersFromTheUpdatedDates()
    {
        _app.Settings.AutoCreateSubscriptionExpenses = true;
        await _app.Subscriptions.AddAsync("Netflix", 1_599, 1, new DateTime(2026, 9, 5));
        await _app.RecurringIncomes.AddAsync("Salaire", 200_000, 1, new DateTime(2026, 10, 1));

        await Startup.PrepareAsync();

        Assert.Equal(2, (await _app.Expenses.GetAllAsync()).Count);
        Assert.Equal(200_000, Assert.Single(await _app.Incomes.GetAllAsync()).AmountMinor);
        // La prochaine échéance (5 novembre) a avancé avant le calcul du rappel : alarme le 2 novembre à 9 h.
        var plan = Assert.Single(_app.Reminders.Plans);
        Assert.Equal(new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero), Assert.Single(plan.Alarms).TriggerAt);
    }

    [Fact]
    public async Task Prepare_SchedulesNothing_AndCancelsPendingAlarms_WhenRemindersAreDisabled()
    {
        _app.Settings.SubscriptionRemindersEnabled = false;
        await _app.Subscriptions.AddAsync("Netflix", 1_599, 1, new DateTime(2026, 10, 25));

        await Startup.PrepareAsync();

        var plan = Assert.Single(_app.Reminders.Plans);
        Assert.Empty(plan.Alarms);
        Assert.Null(plan.Notice);
    }

    [Fact]
    public async Task Prepare_CanRunSeveralTimes_WithoutDuplicatingAnything()
    {
        _app.Settings.AutoCreateSubscriptionExpenses = true;
        await _app.Subscriptions.AddAsync("Netflix", 1_599, 1, new DateTime(2026, 10, 1));

        await Startup.PrepareAsync();
        await Startup.PrepareAsync();

        Assert.Single(await _app.Expenses.GetAllAsync());
    }
}
