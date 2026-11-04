using Finance.Android17.Application;
using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Backup;

/// <summary>Une sauvegarde déchiffrée mais aux valeurs incohérentes est refusée avant de toucher aux données.</summary>
public sealed class BackupSnapshotValidatorTests : IDisposable
{
    private static readonly DateTime Day = new(2026, 10, 2);
    private static readonly Guid FoodId = Guid.NewGuid();

    private readonly InfrastructureHost _host = new();

    public void Dispose() => _host.Dispose();

    private IFinanceDataStore Data => _host.Get<IFinanceDataStore>();

    private static FinanceBackupSnapshot Snapshot(
        IReadOnlyList<CategoryBackup>? categories = null,
        IReadOnlyList<ExpenseBackup>? expenses = null,
        IReadOnlyList<SubscriptionBackup>? subscriptions = null,
        IReadOnlyList<SavingsGoalBackup>? goals = null,
        long initialBalance = 0) =>
        new(2, Day, categories ?? [new(FoodId, "Alimentation", "#2E7D32", 20_000, Day)], expenses ?? [], subscriptions ?? [], [], [], goals ?? [], initialBalance);

    [Fact]
    public async Task AValidSnapshot_IsRestored()
    {
        var expense = new ExpenseBackup(Guid.NewGuid(), 1_250, "EUR", "Pain", Day, FoodId, Day);

        await Data.RestoreSnapshotAsync(Snapshot(expenses: [expense]));

        Assert.Equal("Pain", Assert.Single(await _host.Expenses.GetAllAsync()).Description);
    }

    public static TheoryData<string, FinanceBackupSnapshot> Invalid => new()
    {
        { "couleur", Snapshot(categories: [new(FoodId, "Alimentation", "rouge vif", null, Day)]) },
        { "nom vide", Snapshot(categories: [new(FoodId, "  ", "#2E7D32", null, Day)]) },
        { "nom trop long", Snapshot(categories: [new(FoodId, new string('x', 81), "#2E7D32", null, Day)]) },
        { "budget négatif", Snapshot(categories: [new(FoodId, "Alimentation", "#2E7D32", -5, Day)]) },
        { "catégorie en double", Snapshot(categories: [new(FoodId, "A", "#2E7D32", null, Day), new(FoodId, "B", "#2E7D32", null, Day)]) },
        { "montant démesuré", Snapshot(expenses: [new(Guid.NewGuid(), long.MaxValue, "EUR", "x", Day, FoodId, Day)]) },
        { "devise", Snapshot(expenses: [new(Guid.NewGuid(), 100, "euros", "x", Day, FoodId, Day)]) },
        { "date absurde", Snapshot(expenses: [new(Guid.NewGuid(), 100, "EUR", "x", new DateTime(9999, 1, 1), FoodId, Day)]) },
        { "catégorie inconnue", Snapshot(expenses: [new(Guid.NewGuid(), 100, "EUR", "x", Day, Guid.NewGuid(), Day)]) },
        { "dépense en double", Snapshot(expenses: [new(FoodId, 100, "EUR", "x", Day, FoodId, Day), new(FoodId, 100, "EUR", "y", Day, FoodId, Day)]) },
        { "périodicité", Snapshot(subscriptions: [new(Guid.NewGuid(), "Netflix", 1_599, "EUR", 0, Day, true, Day)]) },
        { "jour d'ancrage", Snapshot(subscriptions: [new(Guid.NewGuid(), "Netflix", 1_599, "EUR", 1, Day, true, Day, AnchorDay: 40)]) },
        { "objectif sans cible", Snapshot(goals: [new(Guid.NewGuid(), "Vacances", 0, 0, null, Day)]) },
        { "solde démesuré", Snapshot(initialBalance: long.MinValue) },
    };

    [Theory]
    [MemberData(nameof(Invalid))]
    public async Task AnInconsistentSnapshot_IsRefused_AndLeavesTheDataAlone(string _, FinanceBackupSnapshot snapshot)
    {
        var before = (await _host.Categories.GetAllAsync()).Count;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Data.RestoreSnapshotAsync(snapshot));

        Assert.StartsWith("Sauvegarde refusée", error.Message);
        Assert.Equal(before, (await _host.Categories.GetAllAsync()).Count);
    }
}
