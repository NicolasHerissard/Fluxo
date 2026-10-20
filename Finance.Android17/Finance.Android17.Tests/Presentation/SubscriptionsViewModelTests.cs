using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Presentation;

public sealed class SubscriptionsViewModelTests : IDisposable
{
    private readonly AppFixture _app = new();

    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task Load_OnAnEmptyDatabase_ShowsEmptyTotals()
    {
        var vm = _app.CreateSubscriptions();

        await vm.LoadAsync();

        Assert.True(vm.IsEmpty);
        Assert.Equal("0,00 € / mois", vm.MonthlyTotal.Fr());
        Assert.Equal("0,00 € / an", vm.AnnualTotal.Fr());
    }

    [Fact]
    public async Task Load_ComputesMonthlyAndAnnualTotals_FromEveryFrequency()
    {
        await _app.Finance.AddSubscriptionAsync("Netflix", 1_599, 1, new DateTime(2026, 10, 25));
        await _app.Finance.AddSubscriptionAsync("Assurance", 9_000, 3, new DateTime(2026, 12, 1));
        await _app.Finance.AddSubscriptionAsync("Antivirus", 4_800, 12, new DateTime(2027, 1, 10));
        var vm = _app.CreateSubscriptions();

        await vm.LoadAsync();

        Assert.Equal(["Antivirus", "Assurance", "Netflix"], vm.Subscriptions.Select(x => x.Name).Order());
        Assert.Equal("Mensuel", vm.Subscriptions.Single(x => x.Name == "Netflix").Frequency);
        Assert.Equal("Tous les 3 mois", vm.Subscriptions.Single(x => x.Name == "Assurance").Frequency);
        Assert.Equal("Annuel", vm.Subscriptions.Single(x => x.Name == "Antivirus").Frequency);
        Assert.Equal("25 oct. 2026", vm.Subscriptions.Single(x => x.Name == "Netflix").NextDate);
        Assert.Equal("15,99 €", vm.Subscriptions.Single(x => x.Name == "Netflix").Amount.Fr());
        // 15,99 + 90,00 / 3 + 48,00 / 12 = 15,99 + 30,00 + 4,00
        Assert.Equal("49,99 € / mois", vm.MonthlyTotal.Fr());
        Assert.Equal("599,88 € / an", vm.AnnualTotal.Fr());
        Assert.False(vm.IsEmpty);
    }

    [Fact]
    public async Task Load_HidesAmounts_WhenPrivacyIsOn()
    {
        await _app.Finance.AddSubscriptionAsync("Netflix", 1_599, 1, new DateTime(2026, 10, 25));
        _app.Settings.HideAmounts = true;
        var vm = _app.CreateSubscriptions();

        await vm.LoadAsync();

        Assert.Equal("••••", vm.Subscriptions.Single().Amount);
        Assert.Equal("•••• / mois", vm.MonthlyTotal);
        Assert.Equal("•••• / an", vm.AnnualTotal);
    }

    [Fact]
    public async Task Load_SchedulesReminders_OnlyWhenEnabled()
    {
        await _app.Finance.AddSubscriptionAsync("Netflix", 1_599, 1, new DateTime(2026, 10, 25));
        _app.Settings.SubscriptionReminderDays = 7;
        var vm = _app.CreateSubscriptions();

        await vm.LoadAsync();

        // J − 7 est déjà passé : pas d'alarme, mais l'échéance est dans la semaine.
        var plan = Assert.Single(_app.Reminders.Plans);
        Assert.Empty(plan.Alarms);
        Assert.Equal("Netflix arrive le 25/10.", plan.Notice?.Text);

        _app.Settings.SubscriptionRemindersEnabled = false;
        await vm.LoadAsync();
        Assert.Single(_app.Reminders.Plans);
    }

    [Fact]
    public async Task Add_UsesTheSelectedFrequency_AndResetsTheForm()
    {
        var vm = _app.CreateSubscriptions();

        vm.Name = "Assurance";
        vm.Amount = "90";
        vm.SelectedFrequency = "Trimestriel";
        vm.NextBillingDate = new DateTime(2026, 12, 1);
        await vm.AddCommand.ExecuteAsync(null);

        var stored = Assert.Single(await _app.Finance.GetSubscriptionsAsync());
        Assert.Equal(3, stored.BillingIntervalMonths);
        Assert.Equal(9_000, stored.AmountMinor);
        Assert.Equal("EUR", stored.CurrencyCode);
        Assert.Equal(string.Empty, vm.Name);
        Assert.Equal("Mensuel", vm.SelectedFrequency);
        Assert.Equal(new DateTime(2026, 11, 20), vm.NextBillingDate);
        Assert.Single(vm.Subscriptions);
    }

    [Theory]
    [InlineData("", "10", "Indiquez le nom de l’abonnement.")]
    [InlineData("Netflix", "", "Indiquez un montant valide.")]
    [InlineData("Netflix", "0", "Indiquez un montant valide.")]
    public async Task Add_ValidatesInput(string name, string amount, string message)
    {
        var vm = _app.CreateSubscriptions();

        vm.Name = name;
        vm.Amount = amount;
        await vm.AddCommand.ExecuteAsync(null);

        Assert.Equal(message, vm.ErrorMessage);
        Assert.Empty(await _app.Finance.GetSubscriptionsAsync());
    }

    [Fact]
    public async Task Edit_ThenSave_UpdatesInsteadOfAdding()
    {
        await _app.Finance.AddSubscriptionAsync("Netflix", 1_599, 1, new DateTime(2026, 10, 25));
        var vm = _app.CreateSubscriptions();
        await vm.LoadAsync();

        vm.EditCommand.Execute(vm.Subscriptions.Single());

        Assert.True(vm.IsEditing);
        Assert.Equal("Modifier l’abonnement", vm.FormTitle);
        Assert.Equal("Enregistrer les modifications", vm.SaveButtonText);
        Assert.Equal("15,99", vm.Amount);

        vm.Amount = "17,99";
        vm.SelectedFrequency = "Annuel";
        await vm.AddCommand.ExecuteAsync(null);

        var stored = Assert.Single(await _app.Finance.GetSubscriptionsAsync());
        Assert.Equal(1_799, stored.AmountMinor);
        Assert.Equal(12, stored.BillingIntervalMonths);
        Assert.False(vm.IsEditing);
        Assert.Equal("Ajouter l’abonnement", vm.SaveButtonText);
    }

    [Fact]
    public async Task Delete_RemovesTheRow_AndCancelsAnOngoingEdit()
    {
        await _app.Finance.AddSubscriptionAsync("Netflix", 1_599, 1, new DateTime(2026, 10, 25));
        var vm = _app.CreateSubscriptions();
        await vm.LoadAsync();
        var row = vm.Subscriptions.Single();
        vm.EditCommand.Execute(row);

        await vm.DeleteCommand.ExecuteAsync(row);

        Assert.Empty(vm.Subscriptions);
        Assert.Empty(await _app.Finance.GetSubscriptionsAsync());
        Assert.False(vm.IsEditing);
        Assert.True(vm.IsEmpty);
    }
}
