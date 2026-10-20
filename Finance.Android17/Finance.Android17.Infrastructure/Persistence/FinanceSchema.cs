using Microsoft.Data.Sqlite;

namespace Finance.Android17.Infrastructure;

/// <summary>
/// Versionnage du schéma par <c>PRAGMA user_version</c>, enregistré dans l'en-tête du fichier. Chaque version est
/// appliquée une seule fois, dans une transaction. Pour changer le schéma : ajouter une migration à
/// <see cref="Migrations"/> (les précédentes ne se modifient jamais), puis adapter <c>FinanceDbContext</c>.
/// </summary>
internal static class FinanceSchema
{
    /// <summary>Version qu'une base à jour porte dans <c>user_version</c>.</summary>
    public static int CurrentVersion => Migrations.Count;

    // L'élément d'indice n fait passer la base de la version n à la version n + 1.
    private static readonly IReadOnlyList<Func<SqliteConnection, CancellationToken, Task>> Migrations = [CreateInitialSchemaAsync];

    /// <exception cref="InvalidOperationException">La base porte une version plus récente que celles que cette application connaît.</exception>
    public static async Task UpgradeAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var version = await ReadVersionAsync(connection, cancellationToken).ConfigureAwait(false);
        if (version > CurrentVersion)
            throw new InvalidOperationException($"La base de données a été créée par une version plus récente de l'application (schéma {version}, schéma connu {CurrentVersion}). Mettez l'application à jour.");

        for (; version < CurrentVersion; version++)
        {
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await Migrations[version](connection, cancellationToken).ConfigureAwait(false);
            await ExecuteAsync(connection, $"PRAGMA user_version = {version + 1}", cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public static async Task<int> ReadVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Version 1 : le schéma tel que les versions précédentes de l'application le laissaient. Sans <c>user_version</c>,
    /// une base est soit neuve, soit déjà créée par l'application mais plus ou moins ancienne : tout est donc créé
    /// seulement s'il manque (catégories et dépenses d'abord, puis abonnements, revenus, objectifs, solde initial, budgets).
    /// </summary>
    private static async Task CreateInitialSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await ExecuteAsync(connection, """
            CREATE TABLE IF NOT EXISTS "Categories" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Categories" PRIMARY KEY,
                "Name" TEXT NOT NULL,
                "Color" TEXT NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "UpdatedAtUtc" TEXT NOT NULL,
                "DeletedAtUtc" TEXT NULL,
                "Version" TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS "Expenses" (
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
            );
            CREATE INDEX IF NOT EXISTS "IX_Expenses_CategoryId" ON "Expenses" ("CategoryId");
            CREATE TABLE IF NOT EXISTS "Subscriptions" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Subscriptions" PRIMARY KEY,
                "Name" TEXT NOT NULL,
                "AmountMinor" INTEGER NOT NULL,
                "CurrencyCode" TEXT NOT NULL,
                "BillingIntervalMonths" INTEGER NOT NULL,
                "NextBillingDate" TEXT NOT NULL,
                "ReminderEnabled" INTEGER NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "UpdatedAtUtc" TEXT NOT NULL,
                "DeletedAtUtc" TEXT NULL,
                "Version" TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS "Incomes" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Incomes" PRIMARY KEY,
                "AmountMinor" INTEGER NOT NULL,
                "CurrencyCode" TEXT NOT NULL,
                "Description" TEXT NOT NULL,
                "OccurredOn" TEXT NOT NULL,
                "RecurringIncomeId" TEXT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "UpdatedAtUtc" TEXT NOT NULL,
                "DeletedAtUtc" TEXT NULL,
                "Version" TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS "RecurringIncomes" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_RecurringIncomes" PRIMARY KEY,
                "Name" TEXT NOT NULL,
                "AmountMinor" INTEGER NOT NULL,
                "CurrencyCode" TEXT NOT NULL,
                "IntervalMonths" INTEGER NOT NULL,
                "NextOccurrenceDate" TEXT NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "UpdatedAtUtc" TEXT NOT NULL,
                "DeletedAtUtc" TEXT NULL,
                "Version" TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS "SavingsGoals" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_SavingsGoals" PRIMARY KEY,
                "Name" TEXT NOT NULL,
                "TargetAmountMinor" INTEGER NOT NULL,
                "SavedAmountMinor" INTEGER NOT NULL,
                "TargetDate" TEXT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "UpdatedAtUtc" TEXT NOT NULL,
                "DeletedAtUtc" TEXT NULL,
                "Version" TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS "FinancialProfiles" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_FinancialProfiles" PRIMARY KEY,
                "InitialBalanceMinor" INTEGER NOT NULL,
                "UpdatedAtUtc" TEXT NOT NULL
            );
            INSERT OR IGNORE INTO "FinancialProfiles" ("Id", "InitialBalanceMinor", "UpdatedAtUtc")
            VALUES (1, 0, CURRENT_TIMESTAMP);
            """, cancellationToken).ConfigureAwait(false);

        if (!await ColumnExistsAsync(connection, "Categories", "BudgetMinor", cancellationToken).ConfigureAwait(false))
            await ExecuteAsync(connection, "ALTER TABLE \"Categories\" ADD COLUMN \"BudgetMinor\" INTEGER NULL", cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> ColumnExistsAsync(SqliteConnection connection, string table, string column, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info($table) WHERE name = $column";
        command.Parameters.AddWithValue("$table", table);
        command.Parameters.AddWithValue("$column", column);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) > 0;
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
