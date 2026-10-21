# Finance.Android17.Tests

Projet de **tests xUnit** (`net10.0`, `IsPackable=false`) de la solution. Il référence `Domain`, `Application`, `Infrastructure` et `Presentation` (jamais `Maui`) et les packages `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `coverlet.collector` et `Microsoft.Extensions.DependencyInjection` (versions dans `../Directory.Packages.props`). `Xunit` est importé globalement par le `.csproj`.

Les tests s'exécutent sur n'importe quel OS avec le SDK .NET 10, sans MAUI ni Android. Une exécution complète dure environ deux minutes : presque tout le temps vient de la dérivation de clé SQLCipher à l'ouverture de chaque base temporaire.

## Organisation

Un dossier par sujet, qui reprend le nom du dossier testé dans `Application`, `Infrastructure` ou `Presentation`.

| Dossier | Ce qu'il vérifie |
| --- | --- |
| `Dashboard/`, `Overview/`, `Expenses/`, `Operations/`, `Subscriptions/`, `Money/`, `Receipts/`, `Recurring/` (planificateur, `Recurrence`), `Reminders/` (planificateur), `Security/PinPolicyTests` | Les **règles pures** d'`Application`, sans base ni conteneur (entités fabriquées par `Support/Samples`). |
| `Domain/` | `Entity.Touch/MarkDeleted/MarkRestored`. |
| `Persistence/` | Les dépôts, `FinanceDataStore`, l'initialisation concurrente, le **schéma versionné** (base neuve, base de la première version, tables ajoutées plus tard, base plus récente refusée) et les catégories par défaut, sur une vraie base SQLCipher temporaire. |
| `Security/` | `PinLockService` et `DatabaseKeyProvider`, dont des vecteurs de référence calculés indépendamment (Python) pour que le format du PIN ne change pas sans qu'on le voie. |
| `Backup/` | `EncryptedBackupService` et le format `.mfbackup` : aller-retour, mauvais mot de passe, fichier altéré, **fichier de référence existant** ; compatibilité du format 1. |
| `Export/` | `CsvExportService`. |
| `Recurring/`, `Reminders/`, `Startup/` | Les services d'orchestration (`DueOperationsService`, `SubscriptionReminderScheduler`, `StartupService`) sur le conteneur complet. |
| `Presentation/` | Les ViewModels (`AppFixture`), `MoneyFormatter`, `BillingFrequency` et la composition (`CompositionTests` : tout se résout avec `AddInfrastructure` + `AddApplication` + `AddPresentation`). |
| `Settings/` | Les valeurs de réglages persistées (`ThemeOptions`, `AfterSaveOptions`). |
| `Support/` | Outils de test (voir ci-dessous). |

### `Support/`

- `TestDoubles.cs` : `FixedTimeProvider` (horloge figée, modifiable par `Now`), faux `ISettingsService`, `ISecureValueStore`, `IFileExchange`, `IAppLockService`, `IBiometricService`, `IOcrService`, `IReminderService`, et `TempDatabase` (dossier temporaire supprimé avec le test).
- `InfrastructureHost` : `AddInfrastructure` sur une base temporaire, avec coffre et échange de fichiers factices et horloge figée. `databaseOf:` rouvre la base d'un autre hôte (simule un redémarrage) ; `ExecuteSqlAsync` / `ScalarAsync` inspectent ou fabriquent une base en SQL.
- `AppFixture` : l'application composée comme en production (`AddInfrastructure` + `AddApplication`) avec réglages et services natifs factices ; fabrique les ViewModels (`CreateDashboard`, `CreateExpenses`…) et un jeu de données commun (`SeedScenarioAsync`).
- `FinancePorts` : accès direct aux ports de persistance (`Expenses`, `Categories`, `Subscriptions`…) pour `AppFixture` et `InfrastructureHost`.
- `Samples` : entités du domaine prêtes à l'emploi pour les tests purs. `TestText` : `Fr()` / `Compact()`, voir ci-dessous.

## Règles

- **« Aujourd'hui » vaut le mardi 20 octobre 2026, 9 h 30** (`AppFixture.Now`). Aucun test ne dépend de l'heure de la machine : toujours passer l'horloge figée (enregistrée comme `TimeProvider`, pas comme sa sous-classe) et ne jamais utiliser `DateTime.Now`/`Today`.
- Une règle pure se teste sans base ; un comportement de persistance passe par `InfrastructureHost` (vraie base, pas de double de dépôt) ; un ViewModel passe par `AppFixture`.
- Les textes fr-FR contiennent des espaces insécables (U+202F, U+00A0) : comparer avec `.Fr()` ; pour un pourcentage, dont le format dépend de la culture, `.Compact()`.
- Une nouvelle règle de calcul, un nouveau dépôt ou une nouvelle migration du schéma vient avec ses tests, **y compris le chemin d'une base existante** pour une migration.
- Ne jamais désactiver ni ignorer un test pour obtenir un résultat vert.
- Les `internal` des autres projets ne sont pas visibles ici : on les teste à travers les types publics (ViewModels, `AddInfrastructure`), sans `InternalsVisibleTo`.
- Montants en centimes (`long`), comme partout.

## Commandes

```bash
dotnet test Finance.Android17.Tests/Finance.Android17.Tests.csproj -c Debug
dotnet test Finance.Android17.Tests/Finance.Android17.Tests.csproj --filter "FullyQualifiedName~Dashboard"   # un sujet
```
