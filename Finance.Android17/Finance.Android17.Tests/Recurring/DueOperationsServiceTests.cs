using Finance.Android17.Application;
using Finance.Android17.Domain;
using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Recurring;

/// <summary>Rattrapage des échéances de bout en bout : règles de l'Application, vraie base SQLCipher.</summary>
public sealed class DueOperationsServiceTests : IDisposable
{
    private readonly AppFixture _app = new();

    public DueOperationsServiceTests() => _app.Settings.AutoCreateSubscriptionExpenses = true;

    public void Dispose() => _app.Dispose();

    private DueOperationsService Service => _app.Get<DueOperationsService>();

    [Fact]
    public async Task CreatesOneExpensePerMissedDate_AndAdvancesTheNextDate()
    {
        await _app.Subscriptions.AddAsync("Netflix", 1299, 1, new DateTime(2026, 8, 15));

        var result = await Service.ApplyAsync();

        Assert.Equal(new DueOperationsResult(3, 0), result);
        var expenses = (await _app.Expenses.GetAllAsync()).OrderBy(x => x.OccurredOn).ToList();
        Assert.Equal([new DateTime(2026, 8, 15), new DateTime(2026, 9, 15), new DateTime(2026, 10, 15)], expenses.Select(x => x.OccurredOn));
        Assert.All(expenses, x =>
        {
            Assert.Equal(1299, x.AmountMinor);
            Assert.Equal("Abonnement · Netflix", x.Description);
            Assert.Equal("Abonnements", x.Category?.Name);
        });
        Assert.Equal(new DateTime(2026, 11, 15), Assert.Single(await _app.Subscriptions.GetAllAsync()).NextBillingDate);
        var category = Assert.Single(await _app.Categories.GetAllAsync(), x => x.Name == DefaultCategories.SubscriptionsName);
        Assert.Equal(DefaultCategories.SubscriptionsColor, category.Color);
    }

    [Fact]
    public async Task IsIdempotent_AndLeavesFutureSubscriptionsAlone()
    {
        await _app.Subscriptions.AddAsync("Journal", 3000, 3, new DateTime(2026, 7, 1));
        await _app.Subscriptions.AddAsync("Assurance", 50_000, 12, new DateTime(2027, 1, 1));

        Assert.Equal(2, (await Service.ApplyAsync()).ExpensesCreated);
        Assert.Equal(0, (await Service.ApplyAsync()).ExpensesCreated);

        var subscriptions = await _app.Subscriptions.GetAllAsync();
        Assert.Equal(new DateTime(2027, 1, 1), subscriptions.Single(x => x.Name == "Journal").NextBillingDate);
        Assert.Equal(new DateTime(2027, 1, 1), subscriptions.Single(x => x.Name == "Assurance").NextBillingDate);
        Assert.Equal(2, (await _app.Expenses.GetAllAsync()).Count);
        Assert.Single(await _app.Categories.GetAllAsync(), x => x.Name == "Abonnements");
    }

    [Fact]
    public async Task WithNothingDue_DoesNotCreateTheSubscriptionsCategory()
    {
        await _app.Subscriptions.AddAsync("Assurance", 50_000, 12, new DateTime(2027, 1, 1));

        Assert.Equal(new DueOperationsResult(0, 0), await Service.ApplyAsync());

        Assert.DoesNotContain(await _app.Categories.GetAllAsync(), x => x.Name == "Abonnements");
    }

    [Fact]
    public async Task ReusesAnExistingSubscriptionsCategory()
    {
        await _app.Categories.AddAsync("Abonnements", "#123456");
        await _app.Subscriptions.AddAsync("Netflix", 1299, 1, new DateTime(2026, 10, 1));

        await Service.ApplyAsync();

        var category = Assert.Single(await _app.Categories.GetAllAsync(), x => x.Name == "Abonnements");
        Assert.Equal("#123456", category.Color);
        Assert.Equal(category.Id, Assert.Single(await _app.Expenses.GetAllAsync()).CategoryId);
    }

    [Fact]
    public async Task FallsBackOnTheConfiguredCurrency_WhenTheSubscriptionHasNone()
    {
        _app.Settings.CurrencyCode = "CHF";
        await _app.Subscriptions.AddAsync("Cloud", 500, 1, new DateTime(2026, 10, 1), "");

        await Service.ApplyAsync();

        Assert.Equal("CHF", Assert.Single(await _app.Expenses.GetAllAsync()).CurrencyCode);
    }

    [Fact]
    public async Task CreatesLinkedIncomes_ForDueRecurringIncomes()
    {
        await _app.RecurringIncomes.AddAsync("Salaire", 250_000, 1, new DateTime(2026, 9, 1));
        var recurringId = Assert.Single(await _app.RecurringIncomes.GetAllAsync()).Id;

        var result = await Service.ApplyAsync();

        Assert.Equal(new DueOperationsResult(0, 2), result);
        var incomes = (await _app.Incomes.GetAllAsync()).OrderBy(x => x.OccurredOn).ToList();
        Assert.Equal([new DateTime(2026, 9, 1), new DateTime(2026, 10, 1)], incomes.Select(x => x.OccurredOn));
        Assert.All(incomes, x =>
        {
            Assert.Equal(recurringId, x.RecurringIncomeId);
            Assert.Equal("Salaire", x.Description);
            Assert.Equal(250_000, x.AmountMinor);
        });
        Assert.Equal(new DateTime(2026, 11, 1), Assert.Single(await _app.RecurringIncomes.GetAllAsync()).NextOccurrenceDate);
        Assert.Equal(0, (await Service.ApplyAsync()).IncomesCreated);
    }

    [Fact]
    public async Task RespectsTheUserSettings()
    {
        _app.Settings.AutoCreateSubscriptionExpenses = false;
        _app.Settings.AutoCreateRecurringIncomes = false;
        await _app.Subscriptions.AddAsync("Netflix", 1299, 1, new DateTime(2026, 10, 1));
        await _app.RecurringIncomes.AddAsync("Salaire", 250_000, 1, new DateTime(2026, 10, 1));

        Assert.Equal(new DueOperationsResult(0, 0), await Service.ApplyAsync());
        Assert.Empty(await _app.Expenses.GetAllAsync());
        Assert.Empty(await _app.Incomes.GetAllAsync());

        _app.Settings.AutoCreateRecurringIncomes = true;
        Assert.Equal(new DueOperationsResult(0, 1), await Service.ApplyAsync());
        Assert.Empty(await _app.Expenses.GetAllAsync());
    }

    [Fact]
    public async Task ApplyDueOperations_WithAnEmptyPlan_ChangesNothing()
    {
        await _app.DueOperations.ApplyAsync(DueOperationsPlan.Empty);

        Assert.Empty(await _app.Expenses.GetAllAsync());
        Assert.DoesNotContain(await _app.Categories.GetAllAsync(), x => x.Name == "Abonnements");
    }
}
