using Finance.Android17.Application;
using Finance.Android17.Domain;
using Finance.Android17.Tests.Support;
using Microsoft.Data.Sqlite;

namespace Finance.Android17.Tests.Persistence;

public sealed class EncryptedFinanceStoreTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly IFinanceStore _store;

    public EncryptedFinanceStoreTests() => _store = _db.CreateStore();

    public void Dispose() => _db.Dispose();

    private async Task<Category> CategoryAsync(string name) =>
        (await _store.GetCategoriesAsync()).Single(x => x.Name == name);

    [Fact]
    public async Task Initialize_SeedsTheFiveDefaultCategories_OnlyOnce()
    {
        await _store.InitializeAsync();
        await _store.InitializeAsync();
        var reopened = _db.CreateStore();

        var names = (await reopened.GetCategoriesAsync()).Select(x => x.Name).ToList();

        Assert.Equal(["Alimentation", "Autre", "Logement", "Loisirs", "Transport"], names);
    }

    [Fact]
    public async Task Database_IsEncrypted_AndUnreadableWithAnotherKey()
    {
        await _store.InitializeAsync();
        await using (var stream = File.OpenRead(_db.Options.DatabasePath))
        {
            var header = new byte[15];
            await stream.ReadExactlyAsync(header);
            Assert.NotEqual("SQLite format 3", System.Text.Encoding.ASCII.GetString(header));
        }

        var intruder = _db.CreateStore("une-autre-cle-0123456789abcdef0123456789=");

        await Assert.ThrowsAsync<SqliteException>(() => intruder.GetCategoriesAsync());
    }

    [Fact]
    public async Task Data_SurvivesReopeningTheDatabase()
    {
        var food = await CategoryAsync("Alimentation");
        await _store.AddExpenseAsync(1250, "Pain", new DateTime(2026, 10, 2), food.Id);

        var reopened = _db.CreateStore();

        var expense = Assert.Single(await reopened.GetExpensesAsync());
        Assert.Equal(1250, expense.AmountMinor);
    }

    [Fact]
    public async Task AddExpense_TrimsDescription_DropsTheTime_AndLoadsTheCategory()
    {
        var food = await CategoryAsync("Alimentation");

        await _store.AddExpenseAsync(1250, "  Pain  ", new DateTime(2026, 10, 2, 17, 45, 0), food.Id, "USD");

        var expense = Assert.Single(await _store.GetExpensesAsync());
        Assert.Equal("Pain", expense.Description);
        Assert.Equal(new DateTime(2026, 10, 2), expense.OccurredOn);
        Assert.Equal("USD", expense.CurrencyCode);
        Assert.Equal("Alimentation", expense.Category?.Name);
    }

    [Fact]
    public async Task GetExpenses_AreSortedByDateThenByCreation_MostRecentFirst()
    {
        var food = await CategoryAsync("Alimentation");
        await _store.AddExpenseAsync(100, "ancienne", new DateTime(2026, 9, 1), food.Id);
        await _store.AddExpenseAsync(200, "recente-1", new DateTime(2026, 10, 1), food.Id);
        await Task.Delay(5);
        await _store.AddExpenseAsync(300, "recente-2", new DateTime(2026, 10, 1), food.Id);

        var descriptions = (await _store.GetExpensesAsync()).Select(x => x.Description).ToList();

        Assert.Equal(["recente-2", "recente-1", "ancienne"], descriptions);
    }

    [Fact]
    public async Task GetExpenses_Filter_AppliesSearchCategoryAndDates()
    {
        var food = await CategoryAsync("Alimentation");
        var transport = await CategoryAsync("Transport");
        await _store.AddExpenseAsync(100, "Baguette", new DateTime(2026, 10, 1), food.Id);
        await _store.AddExpenseAsync(200, "Billet de train", new DateTime(2026, 10, 10), transport.Id);
        await _store.AddExpenseAsync(300, "Baguette", new DateTime(2026, 9, 1), food.Id);

        var result = await _store.GetExpensesAsync(new ExpenseFilter("Bague", food.Id, new DateTime(2026, 10, 1), new DateTime(2026, 10, 31)));

        Assert.Equal(100, Assert.Single(result).AmountMinor);
    }

    [Fact]
    public async Task UpdateExpense_ChangesTheFields_AndRegeneratesTheVersion()
    {
        var food = await CategoryAsync("Alimentation");
        var transport = await CategoryAsync("Transport");
        await _store.AddExpenseAsync(1250, "Pain", new DateTime(2026, 10, 2), food.Id);
        var before = Assert.Single(await _store.GetExpensesAsync());

        await _store.UpdateExpenseAsync(before.Id, 999, "  Train ", new DateTime(2026, 10, 5), transport.Id, "CHF");

        var after = Assert.Single(await _store.GetExpensesAsync());
        Assert.Equal(999, after.AmountMinor);
        Assert.Equal("Train", after.Description);
        Assert.Equal(new DateTime(2026, 10, 5), after.OccurredOn);
        Assert.Equal("Transport", after.Category?.Name);
        Assert.Equal("CHF", after.CurrencyCode);
        Assert.NotEqual(before.Version, after.Version);
    }

    [Fact]
    public async Task SoftDeleteExpense_HidesIt_AndRestoreBringsItBack()
    {
        var food = await CategoryAsync("Alimentation");
        await _store.AddExpenseAsync(1250, "Pain", new DateTime(2026, 10, 2), food.Id);
        var id = Assert.Single(await _store.GetExpensesAsync()).Id;

        await _store.SoftDeleteExpenseAsync(id);
        Assert.Empty(await _store.GetExpensesAsync());

        await _store.RestoreExpenseAsync(id);
        Assert.Single(await _store.GetExpensesAsync());
    }

    [Fact]
    public async Task DeleteCategory_IsRefusedWhileAnActiveExpenseUsesIt_ThenAllowed()
    {
        var food = await CategoryAsync("Alimentation");
        await _store.AddExpenseAsync(1250, "Pain", new DateTime(2026, 10, 2), food.Id);
        var expenseId = Assert.Single(await _store.GetExpensesAsync()).Id;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _store.SoftDeleteCategoryAsync(food.Id));
        Assert.Equal("Cette catégorie est utilisée par une dépense.", error.Message);

        await _store.SoftDeleteExpenseAsync(expenseId);
        await _store.SoftDeleteCategoryAsync(food.Id);
        Assert.DoesNotContain(await _store.GetCategoriesAsync(), x => x.Id == food.Id);
    }

    [Fact]
    public async Task Categories_CanBeAddedUpdatedAndGetABudget()
    {
        await _store.AddCategoryAsync("  Sport ", "#112233", 5000);
        var sport = await CategoryAsync("Sport");
        Assert.Equal("#112233", sport.Color);
        Assert.Equal(5000, sport.BudgetMinor);

        await _store.UpdateCategoryAsync(sport.Id, "Sports", "#445566", null);

        var updated = await CategoryAsync("Sports");
        Assert.Equal("#445566", updated.Color);
        Assert.Null(updated.BudgetMinor);
    }

    [Fact]
    public async Task Incomes_CanBeAddedUpdatedAndSoftDeleted()
    {
        await _store.AddIncomeAsync(250_000, " Salaire ", new DateTime(2026, 10, 1, 8, 0, 0));
        var income = Assert.Single(await _store.GetIncomesAsync());
        Assert.Equal("Salaire", income.Description);
        Assert.Equal(new DateTime(2026, 10, 1), income.OccurredOn);

        await _store.UpdateIncomeAsync(income.Id, 260_000, "Prime", new DateTime(2026, 10, 2));
        var updated = Assert.Single(await _store.GetIncomesAsync());
        Assert.Equal((260_000, "Prime"), (updated.AmountMinor, updated.Description));

        await _store.SoftDeleteIncomeAsync(income.Id);
        Assert.Empty(await _store.GetIncomesAsync());
    }

    [Fact]
    public async Task RecurringIncome_IntervalIsClampedToAtLeastOneMonth()
    {
        await _store.AddRecurringIncomeAsync("Loyer perçu", 50_000, 0, new DateTime(2026, 11, 1));
        var added = Assert.Single(await _store.GetRecurringIncomesAsync());
        Assert.Equal(1, added.IntervalMonths);

        await _store.UpdateRecurringIncomeAsync(added.Id, "Loyer perçu", 50_000, -3, new DateTime(2026, 11, 1));
        Assert.Equal(1, Assert.Single(await _store.GetRecurringIncomesAsync()).IntervalMonths);
    }

    [Fact]
    public async Task ProcessDueSubscriptions_CreatesOneExpensePerMissedDate_AndAdvancesTheNextDate()
    {
        await _store.AddSubscriptionAsync("Netflix", 1299, 1, new DateTime(2026, 8, 15));

        var created = await _store.ProcessDueSubscriptionsAsync(new DateTime(2026, 10, 20), "EUR");

        Assert.Equal(3, created);
        var expenses = (await _store.GetExpensesAsync()).OrderBy(x => x.OccurredOn).ToList();
        Assert.Equal([new DateTime(2026, 8, 15), new DateTime(2026, 9, 15), new DateTime(2026, 10, 15)], expenses.Select(x => x.OccurredOn));
        Assert.All(expenses, x =>
        {
            Assert.Equal(1299, x.AmountMinor);
            Assert.Equal("Abonnement · Netflix", x.Description);
            Assert.Equal("Abonnements", x.Category?.Name);
        });
        Assert.Equal(new DateTime(2026, 11, 15), Assert.Single(await _store.GetSubscriptionsAsync()).NextBillingDate);
        var subscriptionsCategory = Assert.Single(await _store.GetCategoriesAsync(), x => x.Name == "Abonnements");
        Assert.Equal("#7D5260", subscriptionsCategory.Color);
    }

    [Fact]
    public async Task ProcessDueSubscriptions_IsIdempotent_AndLeavesFutureSubscriptionsAlone()
    {
        await _store.AddSubscriptionAsync("Journal", 3000, 3, new DateTime(2026, 7, 1));
        await _store.AddSubscriptionAsync("Assurance", 50_000, 12, new DateTime(2027, 1, 1));

        Assert.Equal(2, await _store.ProcessDueSubscriptionsAsync(new DateTime(2026, 10, 20), "EUR"));
        Assert.Equal(0, await _store.ProcessDueSubscriptionsAsync(new DateTime(2026, 10, 20), "EUR"));

        var subscriptions = await _store.GetSubscriptionsAsync();
        Assert.Equal(new DateTime(2027, 1, 1), subscriptions.Single(x => x.Name == "Journal").NextBillingDate);
        Assert.Equal(new DateTime(2027, 1, 1), subscriptions.Single(x => x.Name == "Assurance").NextBillingDate);
        Assert.Equal(2, (await _store.GetExpensesAsync()).Count);
        Assert.Single(await _store.GetCategoriesAsync(), x => x.Name == "Abonnements");
    }

    [Fact]
    public async Task ProcessDueSubscriptions_WithNothingDue_DoesNotCreateTheSubscriptionsCategory()
    {
        await _store.AddSubscriptionAsync("Assurance", 50_000, 12, new DateTime(2027, 1, 1));

        Assert.Equal(0, await _store.ProcessDueSubscriptionsAsync(new DateTime(2026, 10, 20), "EUR"));

        Assert.DoesNotContain(await _store.GetCategoriesAsync(), x => x.Name == "Abonnements");
    }

    [Fact]
    public async Task ProcessDueSubscriptions_FallsBackOnTheProvidedCurrency_WhenTheSubscriptionHasNone()
    {
        await _store.AddSubscriptionAsync("Cloud", 500, 1, new DateTime(2026, 10, 1), "");

        await _store.ProcessDueSubscriptionsAsync(new DateTime(2026, 10, 20), "CHF");

        Assert.Equal("CHF", Assert.Single(await _store.GetExpensesAsync()).CurrencyCode);
    }

    [Fact]
    public async Task ProcessDueRecurringIncomes_CreatesLinkedIncomes_AndAdvancesTheNextDate()
    {
        await _store.AddRecurringIncomeAsync("Salaire", 250_000, 1, new DateTime(2026, 9, 1));
        var recurringId = Assert.Single(await _store.GetRecurringIncomesAsync()).Id;

        var created = await _store.ProcessDueRecurringIncomesAsync(new DateTime(2026, 10, 20), "EUR");

        Assert.Equal(2, created);
        var incomes = (await _store.GetIncomesAsync()).OrderBy(x => x.OccurredOn).ToList();
        Assert.Equal([new DateTime(2026, 9, 1), new DateTime(2026, 10, 1)], incomes.Select(x => x.OccurredOn));
        Assert.All(incomes, x =>
        {
            Assert.Equal(recurringId, x.RecurringIncomeId);
            Assert.Equal("Salaire", x.Description);
            Assert.Equal(250_000, x.AmountMinor);
        });
        Assert.Equal(new DateTime(2026, 11, 1), Assert.Single(await _store.GetRecurringIncomesAsync()).NextOccurrenceDate);
        Assert.Equal(0, await _store.ProcessDueRecurringIncomesAsync(new DateTime(2026, 10, 20), "EUR"));
    }

    [Fact]
    public async Task InitialBalance_DefaultsToZero_AndCanBeNegative()
    {
        Assert.Equal(0, await _store.GetInitialBalanceAsync());

        await _store.SetInitialBalanceAsync(-12_345);

        Assert.Equal(-12_345, await _store.GetInitialBalanceAsync());
    }

    [Fact]
    public async Task SavingsGoals_AreSortedByTargetDateThenName_AndCanBeUpdatedAndDeleted()
    {
        await _store.AddSavingsGoalAsync("Vacances", 200_000, 50_000, new DateTime(2027, 6, 1, 10, 0, 0));
        await _store.AddSavingsGoalAsync("Voiture", 1_000_000, 0, new DateTime(2027, 1, 1));
        await _store.AddSavingsGoalAsync("Coussin", 300_000, 0, null);

        var goals = await _store.GetSavingsGoalsAsync();
        Assert.Equal(["Coussin", "Voiture", "Vacances"], goals.Select(x => x.Name));
        Assert.Equal(new DateTime(2027, 6, 1), goals.Single(x => x.Name == "Vacances").TargetDate);

        var vacation = goals.Single(x => x.Name == "Vacances");
        await _store.UpdateSavingsGoalAsync(vacation.Id, "Voyage", 250_000, 100_000, null);
        Assert.Equal(0.4, (await _store.GetSavingsGoalsAsync()).Single(x => x.Name == "Voyage").Progress, 5);

        await _store.SoftDeleteSavingsGoalAsync(vacation.Id);
        Assert.DoesNotContain(await _store.GetSavingsGoalsAsync(), x => x.Id == vacation.Id);
    }

    private async Task SeedAllKindsOfDataAsync()
    {
        var food = await CategoryAsync("Alimentation");
        await _store.AddCategoryAsync("Sport", "#112233", 5000);
        await _store.AddExpenseAsync(1250, "Pain", new DateTime(2026, 10, 2), food.Id, "EUR");
        await _store.AddExpenseAsync(999, "Supprimée", new DateTime(2026, 10, 3), food.Id);
        await _store.SoftDeleteExpenseAsync((await _store.GetExpensesAsync()).Single(x => x.Description == "Supprimée").Id);
        await _store.AddIncomeAsync(250_000, "Salaire", new DateTime(2026, 10, 1));
        await _store.AddRecurringIncomeAsync("Loyer perçu", 50_000, 3, new DateTime(2026, 12, 1));
        await _store.AddSavingsGoalAsync("Vacances", 200_000, 50_000, new DateTime(2027, 6, 1));
        await _store.AddSubscriptionAsync("Netflix", 1299, 1, new DateTime(2026, 11, 15));
        await _store.SetInitialBalanceAsync(100_000);
    }

    [Fact]
    public async Task Backup_RoundTrip_RestoresEverything_ExceptSoftDeletedData()
    {
        await SeedAllKindsOfDataAsync();
        var snapshot = await _store.CreateBackupSnapshotAsync();
        Assert.Equal(2, snapshot.FormatVersion);
        Assert.Single(snapshot.Expenses);

        using var other = new TempDatabase();
        var target = other.CreateStore();
        await target.RestoreBackupSnapshotAsync(snapshot);

        Assert.Equal(await _store.GetInitialBalanceAsync(), await target.GetInitialBalanceAsync());
        Assert.Equal((await _store.GetCategoriesAsync()).Select(x => (x.Id, x.Name, x.Color, x.BudgetMinor)),
            (await target.GetCategoriesAsync()).Select(x => (x.Id, x.Name, x.Color, x.BudgetMinor)));
        var expense = Assert.Single(await target.GetExpensesAsync());
        Assert.Equal(("Pain", 1250L), (expense.Description, expense.AmountMinor));
        Assert.Equal("Alimentation", expense.Category?.Name);
        Assert.Equal("Salaire", Assert.Single(await target.GetIncomesAsync()).Description);
        Assert.Equal(3, Assert.Single(await target.GetRecurringIncomesAsync()).IntervalMonths);
        Assert.Equal(0.25, Assert.Single(await target.GetSavingsGoalsAsync()).Progress, 5);
        Assert.Equal(new DateTime(2026, 11, 15), Assert.Single(await target.GetSubscriptionsAsync()).NextBillingDate);
    }

    [Fact]
    public async Task Restore_ReplacesExistingData()
    {
        await SeedAllKindsOfDataAsync();
        var snapshot = await _store.CreateBackupSnapshotAsync();
        var food = await CategoryAsync("Alimentation");
        await _store.AddExpenseAsync(5, "Ajoutée après la sauvegarde", new DateTime(2026, 10, 9), food.Id);

        await _store.RestoreBackupSnapshotAsync(snapshot);

        Assert.Equal("Pain", Assert.Single(await _store.GetExpensesAsync()).Description);
    }

    [Fact]
    public async Task Restore_Format1_StartsNewDataEmpty_AndResetsTheInitialBalance()
    {
        await SeedAllKindsOfDataAsync();
        var category = (await _store.GetCategoriesAsync()).First();
        var v1 = new FinanceBackupSnapshot(1, DateTime.UtcNow,
            [new CategoryBackup(category.Id, category.Name, category.Color, null, DateTime.UtcNow)],
            [new ExpenseBackup(Guid.NewGuid(), 700, "EUR", "Ancienne dépense", new DateTime(2026, 1, 5), category.Id, DateTime.UtcNow)],
            []);

        await _store.RestoreBackupSnapshotAsync(v1);

        Assert.Equal("Ancienne dépense", Assert.Single(await _store.GetExpensesAsync()).Description);
        Assert.Empty(await _store.GetIncomesAsync());
        Assert.Empty(await _store.GetRecurringIncomesAsync());
        Assert.Empty(await _store.GetSavingsGoalsAsync());
        Assert.Equal(0, await _store.GetInitialBalanceAsync());
    }

    [Fact]
    public async Task Restore_RefusesUnknownFormats_AndLeavesTheDataUntouched()
    {
        await SeedAllKindsOfDataAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _store.RestoreBackupSnapshotAsync(new FinanceBackupSnapshot(3, DateTime.UtcNow, [], [], [])));

        Assert.Single(await _store.GetExpensesAsync());
    }

    [Fact]
    public async Task DeleteAllData_RemovesEverything_AndRecreatesTheDefaultCategories()
    {
        await SeedAllKindsOfDataAsync();

        await _store.DeleteAllDataAsync();

        Assert.Empty(await _store.GetExpensesAsync());
        Assert.Empty(await _store.GetIncomesAsync());
        Assert.Empty(await _store.GetRecurringIncomesAsync());
        Assert.Empty(await _store.GetSubscriptionsAsync());
        Assert.Empty(await _store.GetSavingsGoalsAsync());
        Assert.Equal(0, await _store.GetInitialBalanceAsync());
        Assert.Equal(["Alimentation", "Autre", "Logement", "Loisirs", "Transport"], (await _store.GetCategoriesAsync()).Select(x => x.Name));
    }

    [Fact]
    public async Task Initialize_UpgradesADatabaseCreatedByAnOlderVersion()
    {
        await SeedAllKindsOfDataAsync();
        SqliteConnection.ClearAllPools();
        var key = await new FakeSecretStore().GetOrCreateDatabaseKeyAsync();
        await using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _db.Options.DatabasePath,
            Password = key
        }.ToString()))
        {
            await connection.OpenAsync();
            foreach (var sql in new[]
            {
                "DROP TABLE Subscriptions", "DROP TABLE Incomes", "DROP TABLE RecurringIncomes",
                "DROP TABLE SavingsGoals", "DROP TABLE FinancialProfiles", "ALTER TABLE Categories DROP COLUMN BudgetMinor"
            })
            {
                await using var command = connection.CreateCommand();
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync();
            }
        }
        SqliteConnection.ClearAllPools();

        var upgraded = _db.CreateStore();

        Assert.Equal("Pain", Assert.Single(await upgraded.GetExpensesAsync()).Description);
        Assert.Empty(await upgraded.GetSubscriptionsAsync());
        Assert.Empty(await upgraded.GetIncomesAsync());
        Assert.Equal(0, await upgraded.GetInitialBalanceAsync());
        await upgraded.AddCategoryAsync("Nouvelle", "#000000", 1234);
        Assert.Equal(1234, (await upgraded.GetCategoriesAsync()).Single(x => x.Name == "Nouvelle").BudgetMinor);
    }
}
