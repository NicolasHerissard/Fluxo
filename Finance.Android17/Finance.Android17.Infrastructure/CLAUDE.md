# Finance.Android17.Infrastructure

Couche **infrastructure** : la persistance. Bibliothèque `net10.0` qui implémente `IFinanceStore` (défini dans `Application`) avec EF Core 10 sur une base **SQLite chiffrée par SQLCipher**. Références : `Finance.Android17.Application`, `Finance.Android17.Domain` ; NuGet : `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Sqlite.Core`, `SQLitePCLRaw.bundle_e_sqlcipher`.

## Contenu

| Fichier | Rôle |
| --- | --- |
| `EncryptedFinanceStore.cs` | Seule implémentation publique : `EncryptedFinanceStore(ISecretStore secrets) : IFinanceStore`. |
| `FinanceDbContext.cs` | `DbContext` EF Core, **`internal`** : jeux d'entités, longueurs max, clé étrangère `Expense → Category` en `Restrict`, propriétés calculées ignorées. |
| `Class1.cs` | Placeholder de template, sans logique. |

## Fonctionnement de `EncryptedFinanceStore`

- **Initialisation** (`InitializeAsync`, idempotente et protégée par un `SemaphoreSlim`) : `SQLitePCL.Batteries_V2.Init()`, récupération de la clé via `ISecretStore.GetOrCreateDatabaseKeyAsync`, base `finance-v2.db3` dans `Environment.SpecialFolder.LocalApplicationData`, chaîne de connexion avec mot de passe SQLCipher, puis création/ajustement du schéma. Toutes les méthodes publiques l'appellent d'abord.
- **Schéma sans migrations EF** : `EnsureCreatedAsync` + `CREATE TABLE IF NOT EXISTS` bruts pour `Subscriptions`, `Incomes`, `RecurringIncomes`, `SavingsGoals`, `FinancialProfiles` (ligne unique d'id 1 insérée avec `INSERT OR IGNORE`) + `ALTER TABLE "Categories" ADD COLUMN "BudgetMinor"` dont l'erreur « duplicate column » est ignorée. `EnsureCreated` ne modifie pas une base existante : **toute évolution du schéma doit être ajoutée sous forme de SQL idempotent dans `InitializeAsync`**, en plus du modèle EF.
- **Catégories par défaut** : Alimentation, Transport, Logement, Loisirs, Autre, créées si aucune catégorie n'existe (et recréées par `DeleteAllDataAsync`).
- **Contextes courts** : un `FinanceDbContext` neuf par opération (`CreateContext`), lectures en `AsNoTracking`, `ConfigureAwait(false)` partout.
- **Suppressions logiques** : lectures filtrées sur `DeletedAtUtc == null` ; les modifications mettent à jour `UpdatedAtUtc` et, en général, régénèrent `Version`. Supprimer une catégorie lève `InvalidOperationException` tant qu'une dépense active la référence.
- **Échéances** : `ProcessDueSubscriptionsAsync` (crée la catégorie « Abonnements » si besoin, une dépense par échéance manquée, puis avance `NextBillingDate`) et `ProcessDueRecurringIncomesAsync` (idem pour les revenus) sont transactionnelles et s'appuient sur `FinanceService.GetDueDates`.
- **Sauvegarde / restauration** : `CreateBackupSnapshotAsync` produit un `FinanceBackupSnapshot` de format 2 à partir des seules données non supprimées ; `RestoreBackupSnapshotAsync` accepte les formats 1 et 2 (refus de toute autre version), supprime physiquement toutes les données dans une transaction puis réinsère les catégories avant le reste (contrainte de clé étrangère). Le solde initial vaut 0 pour un snapshot de format 1.
- `DeleteAllDataAsync` efface tout, remet le solde initial à 0 et recrée les cinq catégories par défaut.

## Règles de modification

- Ne pas changer le nom de la base, la clé SQLCipher ni le format de sauvegarde sans stratégie de migration explicite.
- Garder `FinanceDbContext` en `internal` ; le reste de la solution ne parle qu'à `IFinanceStore`.
- Nouvelle entité ou propriété persistée : modèle dans `Domain`, configuration dans `FinanceDbContext`, SQL idempotent dans `InitializeAsync`, méthodes dans `IFinanceStore` + `EncryptedFinanceStore`, et prise en compte dans le snapshot de sauvegarde (nouveau `FormatVersion` si nécessaire).
- Conserver les dates sans heure (`.Date`), les montants en centimes et les suppressions logiques dans les lectures et sauvegardes normales.
- Les chaînes de ce projet (messages d'erreur, noms de catégories) sont en français.
