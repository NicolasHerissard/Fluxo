using Finance.Android17.Application;
using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Persistence;

public sealed class FinanceSchemaTests : IDisposable
{
    private const int CurrentVersion = 1;

    private readonly InfrastructureHost _host = new();

    public void Dispose() => _host.Dispose();

    private Task<long> UserVersionAsync() => _host.ScalarAsync<long>("PRAGMA user_version");

    private Task<long> TableCountAsync(string name) =>
        _host.ScalarAsync<long>($"SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '{name}'");

    private Task<long> ColumnCountAsync(string table, string column) =>
        _host.ScalarAsync<long>($"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = '{column}'");

    /// <summary>Le schéma de la toute première version de l'application, avant abonnements, revenus, objectifs et budgets.</summary>
    private static readonly string[] OriginalSchema =
    [
        """
        CREATE TABLE "Categories" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_Categories" PRIMARY KEY,
            "Name" TEXT NOT NULL,
            "Color" TEXT NOT NULL,
            "CreatedAtUtc" TEXT NOT NULL,
            "UpdatedAtUtc" TEXT NOT NULL,
            "DeletedAtUtc" TEXT NULL,
            "Version" TEXT NOT NULL
        )
        """,
        """
        CREATE TABLE "Expenses" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_Expenses" PRIMARY KEY,
            "AmountMinor" INTEGER NOT NULL,
            "CurrencyCode" TEXT NOT NULL,
            "Description" TEXT NOT NULL,
            "OccurredOn" TEXT NOT NULL,
            "CategoryId" TEXT NOT NULL,
            "CreatedAtUtc" TEXT NOT NULL,
            "UpdatedAtUtc" TEXT NOT NULL,
            "DeletedAtUtc" TEXT NULL,
            "Version" TEXT NOT NULL,
            CONSTRAINT "FK_Expenses_Categories_CategoryId" FOREIGN KEY ("CategoryId") REFERENCES "Categories" ("Id") ON DELETE RESTRICT
        )
        """,
        """CREATE INDEX "IX_Expenses_CategoryId" ON "Expenses" ("CategoryId")""",
        """
        INSERT INTO "Categories" VALUES ('11111111-1111-1111-1111-111111111111', 'Courses', '#123456',
            '2025-01-01 10:00:00', '2025-01-01 10:00:00', NULL, '22222222-2222-2222-2222-222222222222')
        """,
        """
        INSERT INTO "Expenses" VALUES ('33333333-3333-3333-3333-333333333333', 1250, 'EUR', 'Ancienne dépense', '2025-01-05 00:00:00',
            '11111111-1111-1111-1111-111111111111', '2025-01-05 09:00:00', '2025-01-05 09:00:00', NULL, '44444444-4444-4444-4444-444444444444')
        """
    ];

    [Fact]
    public async Task NewDatabase_IsCreatedAtTheCurrentVersion_WithEveryTable()
    {
        await _host.Database.InitializeAsync();

        Assert.Equal(CurrentVersion, await UserVersionAsync());
        foreach (var table in new[] { "Categories", "Expenses", "Subscriptions", "Incomes", "RecurringIncomes", "SavingsGoals", "FinancialProfiles" })
            Assert.Equal(1, await TableCountAsync(table));
        Assert.Equal(1, await ColumnCountAsync("Categories", "BudgetMinor"));
        Assert.Equal(1, await _host.ScalarAsync<long>("SELECT COUNT(*) FROM FinancialProfiles"));
    }

    [Fact]
    public async Task UnversionedDatabaseFromTheFirstRelease_IsUpgradedWithoutLosingData()
    {
        await _host.ExecuteSqlAsync(OriginalSchema);
        Assert.Equal(0, await UserVersionAsync());

        await _host.Database.InitializeAsync();

        var expense = Assert.Single(await _host.Expenses.GetAllAsync());
        Assert.Equal(("Ancienne dépense", 1250L, "Courses"), (expense.Description, expense.AmountMinor, expense.Category?.Name));
        Assert.Equal(new DateTime(2025, 1, 5), expense.OccurredOn);
        Assert.Equal(CurrentVersion, await UserVersionAsync());
        Assert.Equal(1, await ColumnCountAsync("Categories", "BudgetMinor"));
        Assert.Equal(1, await TableCountAsync("Subscriptions"));
        Assert.Equal(0, await _host.Profile.GetInitialBalanceAsync());
        // La base contenait déjà une catégorie : les catégories par défaut ne sont pas ajoutées par-dessus.
        Assert.Equal(["Courses"], (await _host.Categories.GetAllAsync()).Select(x => x.Name));

        await _host.Categories.AddAsync("Sport", "#000000", 4200);
        Assert.Equal(4200, (await _host.Categories.GetAllAsync()).Single(x => x.Name == "Sport").BudgetMinor);
        await _host.Subscriptions.AddAsync("Netflix", 1299, 1, new DateTime(2026, 11, 1));
        Assert.Single(await _host.Subscriptions.GetAllAsync());
    }

    [Fact]
    public async Task UnversionedDatabaseMissingTablesAddedLater_IsUpgradedAndKeepsItsData()
    {
        await _host.Database.InitializeAsync();
        var food = (await _host.Categories.GetAllAsync()).Single(x => x.Name == "Alimentation");
        await _host.Expenses.AddAsync(1250, "Pain", new DateTime(2026, 10, 2), food.Id);
        await _host.ExecuteSqlAsync(
            "DROP TABLE Subscriptions", "DROP TABLE Incomes", "DROP TABLE RecurringIncomes", "DROP TABLE SavingsGoals",
            "DROP TABLE FinancialProfiles", "ALTER TABLE Categories DROP COLUMN BudgetMinor", "PRAGMA user_version = 0");
        using var restarted = new InfrastructureHost(databaseOf: _host, secrets: _host.Secrets);

        Assert.Equal("Pain", Assert.Single(await restarted.Expenses.GetAllAsync()).Description);
        Assert.Empty(await restarted.Subscriptions.GetAllAsync());
        Assert.Empty(await restarted.Incomes.GetAllAsync());
        Assert.Equal(0, await restarted.Profile.GetInitialBalanceAsync());
        await restarted.Categories.AddAsync("Nouvelle", "#000000", 1234);
        Assert.Equal(1234, (await restarted.Categories.GetAllAsync()).Single(x => x.Name == "Nouvelle").BudgetMinor);
        Assert.Equal(CurrentVersion, await restarted.ScalarAsync<long>("PRAGMA user_version"));
    }

    [Fact]
    public async Task UpToDateDatabase_IsLeftAsItIs_WhenOpenedAgain()
    {
        await _host.Database.InitializeAsync();
        await _host.Profile.SetInitialBalanceAsync(-500);
        using var restarted = new InfrastructureHost(databaseOf: _host, secrets: _host.Secrets);

        await restarted.Database.InitializeAsync();

        Assert.Equal(CurrentVersion, await UserVersionAsync());
        Assert.Equal(-500, await restarted.Profile.GetInitialBalanceAsync());
        Assert.Equal(5, (await restarted.Categories.GetAllAsync()).Count);
    }

    [Fact]
    public async Task DatabaseFromAMoreRecentVersion_IsRefused_AndNotTouched()
    {
        await _host.Database.InitializeAsync();
        await _host.ExecuteSqlAsync($"PRAGMA user_version = {CurrentVersion + 1}", "CREATE TABLE FuturFeature (Id INTEGER)");
        using var restarted = new InfrastructureHost(databaseOf: _host, secrets: _host.Secrets);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.Database.InitializeAsync());

        Assert.Contains("version plus récente", error.Message);
        Assert.Equal(CurrentVersion + 1, await UserVersionAsync());
        Assert.Equal(1, await TableCountAsync("FuturFeature"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.Expenses.GetAllAsync());
    }
}
