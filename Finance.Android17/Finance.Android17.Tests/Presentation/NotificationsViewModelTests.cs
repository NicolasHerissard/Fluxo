using Finance.Android17.Presentation;
using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Presentation;

public sealed class NotificationsViewModelTests : IDisposable
{
    private readonly AppFixture _app = new();

    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task Load_WithoutAnyAlert_ShowsTheEmptyState()
    {
        var vm = _app.CreateNotifications();

        await vm.LoadAsync();

        Assert.True(vm.IsEmpty);
        Assert.False(vm.HasUnread);
        Assert.Empty(vm.Items);
        Assert.Equal("Aucune alerte pour le moment.", vm.Summary);
    }

    [Fact]
    public async Task Load_ListsBudgetAndSubscriptionAlerts_AsNewRows()
    {
        await _app.SeedScenarioAsync();
        _app.Settings.MonthlyBudgetMinor = 25_000;
        var vm = _app.CreateNotifications();

        await vm.LoadAsync();

        Assert.False(vm.IsEmpty);
        Assert.True(vm.HasUnread);
        Assert.Equal("3 alertes à consulter.", vm.Summary);
        var budget = vm.Items.Single(x => x.Title == "Budget mensuel bientôt atteint");
        Assert.Equal("202,50€dépenséssur250,00€(81%).", budget.Message.Compact());
        Assert.Equal("warning", budget.Severity);
        var food = vm.Items.Single(x => x.Title == "Budget « Alimentation » bientôt atteint");
        Assert.Equal("wallet", food.Icon);
        Assert.Equal("Prélèvement dans 5 jours", vm.Items.Single(x => x.Icon == "calendar").Title);
        Assert.All(vm.Items, x => Assert.True(x.IsNew));
    }

    [Fact]
    public async Task MarkAllRead_ClearsTheUnreadFlag_UntilAnotherAlertAppears()
    {
        await _app.SeedScenarioAsync();
        var vm = _app.CreateNotifications();
        await vm.LoadAsync();
        Assert.True(vm.HasUnread);

        vm.MarkAllReadCommand.Execute(null);

        Assert.False(vm.HasUnread);
        Assert.All(vm.Items, x => Assert.False(x.IsNew));

        var reopened = _app.CreateNotifications();
        await reopened.LoadAsync();
        Assert.False(reopened.HasUnread);
        Assert.NotEmpty(reopened.Items);

        _app.Settings.MonthlyBudgetMinor = 20_000;
        await reopened.LoadAsync();
        Assert.True(reopened.HasUnread);
        Assert.Contains(reopened.Items, x => x.Title == "Budget mensuel dépassé" && x.IsNew);
        Assert.Contains(reopened.Items, x => x.Icon == "calendar" && !x.IsNew);
    }

    [Fact]
    public async Task Load_MasksAmounts_WhenTheyAreHidden()
    {
        await _app.SeedScenarioAsync();
        _app.Settings.HideAmounts = true;
        var vm = _app.CreateNotifications();

        await vm.LoadAsync();

        Assert.Contains(vm.Items, x => x.Message == "Netflix · ••••");
    }

    [Fact]
    public async Task Load_PutsTheMostSevereAlertsFirst()
    {
        var food = await _app.CategoryAsync("Alimentation");
        await _app.Expenses.AddAsync(10_000, "Courses", new DateTime(2026, 10, 3), food.Id);
        await _app.Subscriptions.AddAsync("Netflix", 1_599, 1, new DateTime(2026, 10, 22));
        var vm = _app.CreateNotifications();

        await vm.LoadAsync();

        Assert.Equal("Découvert probable en fin de mois", vm.Items[0].Title);
        Assert.Equal("critical", vm.Items[0].Severity);
        Assert.Equal("Prélèvement dans 2 jours", vm.Items[^1].Title);
    }
}
