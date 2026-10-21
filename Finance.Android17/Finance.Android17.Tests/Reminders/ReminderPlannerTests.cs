using Finance.Android17.Application;
using Finance.Android17.Domain;
using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Reminders;

public sealed class ReminderPlannerTests
{
    // Mardi 20 octobre 2026, 9 h 30, fuseau UTC.
    private static readonly TimeProvider Clock = new FixedTimeProvider(AppFixture.Now);

    private static Subscription Subscription(string name, DateTime next, bool reminder = true) =>
        new() { Name = name, NextBillingDate = next, ReminderEnabled = reminder };

    [Fact]
    public void Alarm_RingsAtNineAm_NDaysBeforeTheDueDate()
    {
        var netflix = Subscription("Netflix", new DateTime(2026, 10, 25));

        var plan = ReminderPlanner.Plan([netflix], 3, Clock);

        var alarm = Assert.Single(plan.Alarms);
        Assert.Equal(netflix.Id, alarm.SubscriptionId);
        Assert.Equal(new DateTimeOffset(2026, 10, 22, 9, 0, 0, TimeSpan.Zero), alarm.TriggerAt);
        Assert.Equal("Netflix arrive le 25/10.", alarm.Message);
        Assert.Null(plan.Notice);
    }

    [Fact]
    public void Alarm_IsSkipped_WhenItsTimeHasPassed()
    {
        // J − 3 = 18 octobre 9 h : déjà passé. L'échéance du 21 est pourtant imminente.
        var plan = ReminderPlanner.Plan([Subscription("Netflix", new DateTime(2026, 10, 21))], 3, Clock);

        Assert.Empty(plan.Alarms);
        Assert.NotNull(plan.Notice);
    }

    [Fact]
    public void Alarm_IsSkipped_WhenItsTimeIsEarlierToday()
    {
        // Échéance dans 0 jour à 9 h, il est 9 h 30 : trop tard pour l'alarme.
        var plan = ReminderPlanner.Plan([Subscription("Netflix", new DateTime(2026, 10, 20))], 0, Clock);

        Assert.Empty(plan.Alarms);
        Assert.Equal("Netflix arrive le 20/10.", plan.Notice?.Text);
    }

    [Fact]
    public void Subscriptions_WithRemindersTurnedOff_AreIgnored()
    {
        var plan = ReminderPlanner.Plan([Subscription("Netflix", new DateTime(2026, 10, 22), reminder: false)], 3, Clock);

        Assert.Empty(plan.Alarms);
        Assert.Null(plan.Notice);
    }

    [Fact]
    public void Notice_NamesTheSubscription_WhenOneIsComingUp()
    {
        var plan = ReminderPlanner.Plan([Subscription("Netflix", new DateTime(2026, 10, 22)), Subscription("Assurance", new DateTime(2026, 12, 1))], 3, Clock);

        Assert.Equal("Échéance à venir", plan.Notice?.Title);
        Assert.Equal("Netflix arrive le 22/10.", plan.Notice?.Text);
        Assert.Equal("subscription_reminder_20261020", plan.Notice?.Key);
    }

    [Fact]
    public void Notice_CountsTheSubscriptions_WhenSeveralAreComingUp()
    {
        var plan = ReminderPlanner.Plan([Subscription("Netflix", new DateTime(2026, 10, 22)), Subscription("Spotify", new DateTime(2026, 10, 23))], 3, Clock);

        Assert.Equal("2 abonnements arrivent dans les 3 prochains jours.", plan.Notice?.Text);
    }

    [Fact]
    public void Notice_ConsidersTodayAndTheLastDayOfTheWindow()
    {
        var plan = ReminderPlanner.Plan([Subscription("Aujourd'hui", new DateTime(2026, 10, 20)), Subscription("Limite", new DateTime(2026, 10, 23)), Subscription("Hier", new DateTime(2026, 10, 19)), Subscription("Trop tard", new DateTime(2026, 10, 24))], 3, Clock);

        Assert.Equal("2 abonnements arrivent dans les 3 prochains jours.", plan.Notice?.Text);
    }

    [Fact]
    public void NegativeReminderDays_AreTreatedAsZero()
    {
        var plan = ReminderPlanner.Plan([Subscription("Netflix", new DateTime(2026, 10, 25))], -5, Clock);

        Assert.Equal(new DateTimeOffset(2026, 10, 25, 9, 0, 0, TimeSpan.Zero), Assert.Single(plan.Alarms).TriggerAt);
    }

    [Fact]
    public void NoSubscription_ProducesAnEmptyPlan()
    {
        var plan = ReminderPlanner.Plan([], 3, Clock);

        Assert.Empty(plan.Alarms);
        Assert.Null(plan.Notice);
    }
}
