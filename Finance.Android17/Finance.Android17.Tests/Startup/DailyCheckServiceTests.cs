using Finance.Android17.Application;
using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Startup;

public sealed class DailyCheckServiceTests : IDisposable
{
    private readonly AppFixture _app = new();

    public void Dispose() => _app.Dispose();

    private DailyCheckService Check => _app.Get<DailyCheckService>();

    [Theory]
    [InlineData(9, 30, 21, 8, 0)]   // après l'heure du contrôle : demain matin
    [InlineData(7, 0, 20, 8, 0)]    // avant : le jour même
    [InlineData(7, 58, 20, 8, 0)]
    [InlineData(7, 59, 21, 8, 0)]   // à moins d'une minute de l'heure : le contrôle du jour serait déjà passé
    public void NextRun_IsTheNextCheckHour(int hour, int minute, int expectedDay, int expectedHour, int expectedMinute)
    {
        var clock = new FixedTimeProvider(new DateTime(2026, 10, 20, hour, minute, 0));

        var next = DailyCheckService.NextRun(clock);

        Assert.Equal(new DateTimeOffset(2026, 10, expectedDay, expectedHour, expectedMinute, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public async Task Run_PublishesTheNewBudgetAlerts_ButNotTheUpcomingDebits()
    {
        await _app.SeedScenarioAsync();   // Alimentation à 81 % de son budget ; Netflix prélevé dans 5 jours

        await Check.RunAsync();

        var alert = Assert.Single(Assert.Single(_app.Alerts.Batches));
        Assert.Equal(NotificationTopic.CategoryBudget, alert.Topic);
        Assert.Equal("Alimentation", alert.Subject);
        Assert.Equal(NotificationSeverity.Warning, alert.Severity);
    }

    [Fact]
    public async Task Run_DoesNotRepeatAnAlertAlreadyPublished()
    {
        await _app.SeedScenarioAsync();

        await Check.RunAsync();
        await Check.RunAsync();

        Assert.Single(_app.Alerts.Batches);
    }

    [Fact]
    public async Task Run_PublishesAgain_WhenAnAlertGetsWorse()
    {
        await _app.SeedScenarioAsync();
        await Check.RunAsync();
        var food = await _app.CategoryAsync("Alimentation");
        await _app.Expenses.AddAsync(5_000, "Traiteur", new DateTime(2026, 10, 18), food.Id);   // 212,50 € sur 200 €

        await Check.RunAsync();

        Assert.Equal(2, _app.Alerts.Batches.Count);
        var alert = Assert.Single(_app.Alerts.Batches[1]);
        Assert.Equal(NotificationSeverity.Critical, alert.Severity);
    }

    [Fact]
    public async Task Run_SkipsAlertsTheUserAlreadySawInTheApp()
    {
        await _app.SeedScenarioAsync();
        var notifications = _app.Get<NotificationService>();
        notifications.MarkRead((await notifications.LoadAsync()).Items);

        await Check.RunAsync();

        Assert.Empty(_app.Alerts.Batches);
    }

    [Fact]
    public async Task Run_PublishesNothing_WhenBackgroundAlertsAreOff_ButStillCatchesUpDueOperations()
    {
        _app.Settings.BackgroundAlertsEnabled = false;
        _app.Settings.AutoCreateSubscriptionExpenses = true;
        await _app.SeedScenarioAsync();
        await _app.Subscriptions.AddAsync("Spotify", 999, 1, new DateTime(2026, 10, 1));

        await Check.RunAsync();

        Assert.Empty(_app.Alerts.Batches);
        Assert.Equal(5, (await _app.Expenses.GetAllAsync()).Count);
    }

    [Fact]
    public async Task Run_RetriesLater_WhenThePlatformRefusesToNotify()
    {
        await _app.SeedScenarioAsync();
        _app.Alerts.Accepts = false;

        await Check.RunAsync();

        Assert.Empty(_app.Alerts.Batches);
        Assert.Equal(string.Empty, _app.Settings.PushedNotifications);

        _app.Alerts.Accepts = true;
        await Check.RunAsync();

        Assert.Single(Assert.Single(_app.Alerts.Batches));
    }

    [Fact]
    public async Task Run_ForgetsAlertsThatAreGone_SoTheyCanComeBack()
    {
        await _app.SeedScenarioAsync();
        await Check.RunAsync();
        Assert.NotEqual(string.Empty, _app.Settings.PushedNotifications);

        _app.Clock.Now = new DateTime(2026, 11, 3, 9, 30, 0);   // un autre mois : plus aucune alerte de budget
        await Check.RunAsync();

        Assert.Equal(string.Empty, _app.Settings.PushedNotifications);
    }

    [Fact]
    public async Task Run_PreparesLikeAnUnlock_WithoutTheImmediateNotice_AndSchedulesTheNextCheck()
    {
        // Netflix tombe dans la fenêtre de rappel (3 jours) : au premier plan, la notification du jour s'afficherait.
        await _app.Subscriptions.AddAsync("Netflix", 1_599, 1, new DateTime(2026, 10, 22));

        await Check.RunAsync();

        var plan = Assert.Single(_app.Reminders.Plans);
        Assert.Null(plan.Notice);
        Assert.Equal(1, _app.DailyChecks.Calls);
    }

    [Theory]
    [InlineData("2026-10-19", 9, 30, true)]    // contrôle d'hier seulement, 8 h passée : celui d'aujourd'hui est rattrapé
    [InlineData("2026-10-19", 7, 59, true)]    // application ouverte à une minute de l'heure : le contrôle du jour ne doit pas être perdu
    [InlineData("2026-10-20", 9, 30, false)]   // déjà fait aujourd'hui : demain
    [InlineData("2026-10-19", 7, 0, false)]    // pas encore l'heure : le jour même, comme d'habitude
    [InlineData("", 9, 30, false)]             // première utilisation : rien à rattraper
    [InlineData("n'importe quoi", 9, 30, false)]
    public void NextRun_CatchesUpATodayCheckThatDidNotHappen(string lastCheckDay, int hour, int minute, bool catchesUp)
    {
        var clock = new FixedTimeProvider(new DateTime(2026, 10, 20, hour, minute, 0));

        var next = DailyCheckService.NextRun(clock, lastCheckDay);

        Assert.Equal(catchesUp ? clock.GetLocalNow().AddMinutes(1) : DailyCheckService.NextRun(clock), next);
    }

    [Fact]
    public async Task Run_RecordsTheDayOfTheCheck_BeforePreparing()
    {
        await Check.RunAsync();

        Assert.Equal("2026-10-20", _app.Settings.LastDailyCheckDay);
    }
}
