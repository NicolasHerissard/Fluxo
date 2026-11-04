# Finance.Android17.Infrastructure

Couche **infrastructure** : tout ce qui est indépendant de la plateforme mais dépend d'une technologie. La persistance (EF Core 10 sur une base **SQLite chiffrée par SQLCipher**), la sécurité (PIN, clé de la base), la sauvegarde chiffrée et l'export CSV. Bibliothèque `net10.0`, qui compile et se teste sous Linux. Références : `Finance.Android17.Application`, `Finance.Android17.Domain` ; NuGet : `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Sqlite.Core`, `SQLitePCLRaw.bundle_e_sqlcipher`, `Microsoft.Extensions.DependencyInjection.Abstractions`.

**Seul `InfrastructureServiceCollectionExtensions.AddInfrastructure(FinanceDatabaseOptions? = null)` est public** (avec `FinanceDatabaseOptions`) : dépôts, services et `FinanceDbContext` sont `internal`. L'hôte (`Maui`, ou les tests) fournit seulement `ISecureValueStore` et `IFileExchange`.

## Contenu

| Fichier | Rôle |
| --- | --- |
| `InfrastructureServiceCollectionExtensions.cs` | `AddInfrastructure()` : enregistre en singleton la base, les dépôts, `IDueOperationsStore`, `IFinanceDataStore`, `ISecretStore`, `IAppLockService`, `IExportService`, `IDataBackupService` (et `TimeProvider.System` s'il manque). |
| `Persistence/FinanceDatabaseOptions.cs` | Chemin du fichier de base. `Default()` = `finance-v2.db3` dans `LocalApplicationData` (nom historique : ne pas le changer). |
| `Persistence/FinanceDatabase.cs` | Point d'entrée unique vers la base (implémente `IDatabaseInitializer`) : clé, connexion, mise à niveau du schéma, catégories par défaut, puis fabrique de `FinanceDbContext`. Porte l'horloge (`UtcNow`). |
| `Persistence/FinanceSchema.cs` | Versionnage du schéma par `PRAGMA user_version` et migrations. |
| `Persistence/FinanceDbContext.cs` | `DbContext` EF Core : jeux d'entités, longueurs max, clé étrangère `Expense → Category` en `Restrict`, propriétés calculées ignorées. |
| `Persistence/Repository.cs` | Base commune des dépôts d'entités : ajout, modification, suppression logique et restauration, datés par l'horloge. |
| `Persistence/*Repository.cs` | Un dépôt par agrégat : dépenses, catégories, revenus, revenus récurrents, abonnements, objectifs ; `FinancialProfileRepository` pour le solde initial. |
| `Persistence/DueOperationsStore.cs` | `IDueOperationsStore` : enregistre en une transaction le plan des échéances. |
| `Persistence/FinanceDataStore.cs` | `IFinanceDataStore` : instantané, restauration et remise à zéro. |
| `Security/DatabaseKeyProvider.cs` | `ISecretStore` : clé SQLCipher de 32 octets (Base64) créée au premier lancement, relue dans `ISecureValueStore` (clé de stockage `database_key_v2`). Si le coffre ne la restitue plus alors que le fichier de base existe, lève `DatabaseKeyUnavailableException` au lieu d'en recréer une (la base deviendrait illisible). |
| `Security/DatabaseRecovery.cs` | `IDatabaseRecovery` : sortie de secours quand la clé est perdue. Renomme la base illisible en `finance-v2.db3.illisible-<horodatage>` (jamais supprimée) pour qu'une base neuve soit créée ; refuse si la clé est en réalité disponible. L'interface le propose après confirmation explicite. |
| `Security/PinLockService.cs` | `IAppLockService` : PIN dérivé en PBKDF2-SHA256 (100 000 itérations, sel de 16 octets, hachage de 32 octets, comparaison à temps constant, calcul hors du thread appelant) ; clés de stockage `pin_salt_v2` et `pin_hash_v2`. Limite d'essais : compteur d'échecs persistant (`pin_attempts_v1`), attente de 30 s dès le 5e échec puis doublée jusqu'à 15 min, `VerifyPinAsync` refuse tout PIN pendant l'attente ; vérification et décompte sont sérialisés (`SemaphoreSlim`), deux essais simultanés comptent donc deux échecs. Politique : `PinPolicy` (4 à 8 chiffres). |
| `Backup/BackupCodec.cs` | Format `.mfbackup` : `MFBAK01` ∥ sel 16 o ∥ nonce 12 o ∥ tag 16 o ∥ données, AES-GCM (le magic sert de données associées), clé PBKDF2-SHA256 de 32 octets (200 000 itérations), mot de passe de 12 caractères minimum à la **création** (à la restauration seul un mot de passe non vide est exigé : les sauvegardes faites avec l'ancien minimum de 8 caractères restent lisibles). |
| `Backup/BackupSnapshotValidator.cs` | Contrôle le contenu d'une sauvegarde avant restauration (identifiants uniques, longueurs, couleurs `#RRGGBB`, devises à 3 lettres, montants bornés à 10^13 centimes, périodicités, jours d'ancrage, dates 1900–2200, catégories référencées). Un refus lève `InvalidOperationException` (« Sauvegarde refusée : … ») sans toucher aux données. Appelé par `FinanceDataStore.RestoreSnapshotAsync`. |
| `Backup/EncryptedBackupService.cs` | `IDataBackupService` : instantané JSON → `BackupCodec` → `IFileExchange` ; et l'inverse à l'import. |
| `Export/CsvExportService.cs` | `IExportService` : CSV séparé par `;`, UTF-8 avec BOM, `\r\n`, guillemets doublés, montants en `0.00` invariant. |

## Fonctionnement de la base

- **Initialisation unique** : `FinanceDatabase` ouvre la base à la première utilisation d'un dépôt (ou par `IDatabaseInitializer.InitializeAsync`, appelé au déverrouillage), sous un verrou. Le contexte n'est publié qu'**après** la mise à niveau du schéma et l'ajout des catégories par défaut : un appelant concurrent n'obtient jamais une base à moitié préparée, et un échec (mauvaise clé, fichier illisible) laisse l'initialisation rejouable.
- **Schéma versionné** : `PRAGMA user_version` (dans l'en-tête du fichier) vaut la version du schéma. `FinanceSchema.Migrations` est une liste ordonnée ; chaque élément fait passer la base de la version `n` à `n + 1` dans une transaction. La **version 1** reprend, figé, le schéma laissé par les versions précédentes de l'application : elle crée ce qui manque (`CREATE … IF NOT EXISTS`, colonne `BudgetMinor` ajoutée si absente), donc une base neuve **ou** ancienne et non versionnée (`user_version` = 0) arrive à la version 1 sans perte. La **version 2** ajoute `AnchorDay` (jour du mois voulu) aux abonnements et revenus récurrents, repris du jour de leur prochaine échéance, pour qu'un 31 ne dérive plus vers le 28. Une base dont la version dépasse celle que l'application connaît est **refusée** (`InvalidOperationException`) sans être modifiée.
- **Catégories par défaut** : `DefaultCategories.CreateInitialSet()` (Domain), ajoutées si la table `Categories` est vide, et recréées par `IFinanceDataStore.DeleteAllAsync`.
- **Contextes courts** : un `FinanceDbContext` neuf par opération, lectures en `AsNoTracking`, `ConfigureAwait(false)` partout.
- **Suppressions logiques** : les lectures filtrent `DeletedAtUtc == null`. Toute modification, suppression ou restauration passe par `Entity.Touch/MarkDeleted/MarkRestored` : elle date `UpdatedAtUtc` avec l'horloge injectée et **renouvelle `Version`**. Modifier une entité supprimée ou inconnue ne fait rien. Supprimer une catégorie lève `InvalidOperationException` tant qu'une dépense active l'utilise.
- **Normalisation à l'écriture** : descriptions et noms sans espaces de début/fin, dates ramenées à minuit, intervalle d'abonnement ou de revenu récurrent d'au moins 1 mois.
- **Échéances** (`DueOperationsStore`) : en une transaction, crée la catégorie « Abonnements » si besoin, ajoute les dépenses et revenus du plan et avance les prochaines échéances. Un plan vide ne touche à rien. Le calcul du plan est dans `Application` (`DueOperationsPlanner`).
- **Sauvegarde** (`FinanceDataStore`) : l'instantané est de format **2** et ne contient que les données non supprimées ; la restauration accepte les formats 1 et 2 (autre version : refus, rien n'est modifié), supprime tout puis réinsère les catégories avant le reste, **dans une transaction annulée entièrement en cas d'échec**. Le solde initial vaut 0 pour un format 1.

## Règles de modification

- Ne pas changer le nom de la base, la clé SQLCipher, les clés de stockage sécurisé ni le format `.mfbackup` sans stratégie de migration explicite. Les tests contiennent des vecteurs de référence calculés indépendamment (PIN, sauvegarde) : s'ils échouent, c'est le format qui a changé.
- **Nouvelle entité ou propriété persistée** : modèle dans `Domain` ; configuration dans `FinanceDbContext` ; **une nouvelle migration** à la fin de `FinanceSchema.Migrations` (ne jamais modifier une migration existante : les bases déjà en version 1 ne la rejoueraient pas) ; méthodes dans le port de `Application` et dans le dépôt ; prise en compte dans `FinanceBackupSnapshot` / `FinanceDataStore` (nouveau `FormatVersion` si le contenu change) ; tests de schéma (`FinanceSchemaTests`).
- Un nouveau dépôt hérite de `Repository<T>`, reçoit `FinanceDatabase`, n'appelle jamais l'horloge système directement (`database.UtcNow`), est enregistré dans `AddInfrastructure` et reste `internal`.
- Garder `FinanceDbContext` en `internal` : le reste de la solution ne parle qu'aux ports d'`Application`.
- Conserver dates sans heure, montants en centimes et suppressions logiques : une lecture n'expose jamais une entité dont `DeletedAtUtc` est renseigné, sauf fonctionnalité explicite (par exemple une future corbeille).
- Les chaînes de ce projet (messages d'erreur, noms de catégories) sont en français.
