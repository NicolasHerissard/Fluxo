using Finance.Android17.Application;
using Finance.Android17.Domain;
using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Reminders;

public sealed class SubscriptionReminderSchedulerTests
{
    private readonly FakeSettings _settings = new();
    private readonly FakeReminderService _reminders = new();

    private SubscriptionReminderScheduler Scheduler => new(_settings, _reminders, new FixedTimeProvider(AppFixture.Now));

    private static Subscription Netflix => new() { Name = "Netflix", NextBillingDate = new DateTime(2026, 10, 25) };

    [Fact]
    public async Task Refresh_PlansWithTheConfiguredNumberOfDays()
    {
        _settings.SubscriptionReminderDays = 1;

        await Scheduler.RefreshAsync([Netflix]);

        var plan = Assert.Single(_reminders.Plans);
        Assert.Equal(new DateTimeOffset(2026, 10, 24, 9, 0, 0, TimeSpan.Zero), Assert.Single(plan.Alarms).TriggerAt);
    }

    [Fact]
    public async Task Refresh_AppliesAnEmptyPlan_ToCancelPendingAlarms_WhenRemindersAreDisabled()
    {
        _settings.SubscriptionRemindersEnabled = false;

        await Scheduler.RefreshAsync([Netflix]);

        var plan = Assert.Single(_reminders.Plans);
        Assert.Empty(plan.Alarms);
        Assert.Null(plan.Notice);
        Assert.False(Scheduler.IsEnabled);
    }
}
