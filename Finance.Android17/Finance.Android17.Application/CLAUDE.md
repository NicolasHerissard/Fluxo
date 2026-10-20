# Finance.Android17.Application

Couche **application** : les règles métier, les calculs, les services qui orchestrent les cas d'usage et les **ports** (interfaces) que `Infrastructure` et `Maui` implémentent. Bibliothèque `net10.0`. Références : `Finance.Android17.Domain` ; NuGet : `Microsoft.Extensions.DependencyInjection.Abstractions` (uniquement pour `AddApplication`).

Tout ce qui est calcul est une **fonction pure** (données en entrée, résultat en sortie, horloge passée en paramètre) : aucune dépendance à EF Core, MAUI, Android ni à l'heure de la machine. C'est ce qui permet de les tester sans base ni appareil.

Un seul espace de noms, `Finance.Android17.Application`, pour tout le projet : les dossiers ne servent qu'à ranger. (Il porte le même nom que `Application` de MAUI et d'Android : d'où quelques noms pleinement qualifiés dans `Maui`.)

## Contenu, par dossier

| Dossier | Contenu |
| --- | --- |
| `Expenses/` | `IExpenseRepository` ; `ExpenseSearch` + `ExpenseCriteria` + `ExpensePeriods` (recherche sans accent ni casse, filtres, six tris, tri alphabétique français) ; `CategoryChoice` (catégories triées par usage récent, présélection) ; `Uncategorized` (nom et couleur de « Sans catégorie »). |
| `Categories/` | `ICategoryRepository`. |
| `Incomes/` | `IIncomeRepository`, `IRecurringIncomeRepository`. |
| `Subscriptions/` | `ISubscriptionRepository` ; `SubscriptionCosts` (coût mensuel et annuel). |
| `Savings/` | `ISavingsGoalRepository`. |
| `Overview/` | `IFinancialProfileRepository` (solde initial) ; `FinanceData` + `FinanceDataLoader` (lecture groupée) ; `FinancialSummaryCalculator` → `FinancialSummary` (mois en cours, solde actuel, prévision de fin de mois, taux d'épargne). |
| `Dashboard/` | `DashboardService` (charge puis calcule) ; `DashboardCalculator` → `DashboardReport` ; `PeriodRange` (semaine du lundi, mois, année + période précédente entière), `PeriodComparison`, `BudgetStatus` (alerte à 80 % puis 100 %). |
| `Operations/` | `OperationsFeed` : revenus et dépenses mêlés, du plus récent au plus ancien. |
| `Money/` | `AmountParser` : lecture d'un montant saisi (virgule ou point, arrondi `AwayFromZero`, refuse ce qui dépasserait un `long` en centimes). |
| `Recurring/` | `Recurrence` (échéances manquées), `DueOperationsPlanner` (plan des dépenses/revenus à créer), `DueOperationsService` (selon les réglages), `IDueOperationsStore`. |
| `Reminders/` | `ReminderPlanner` (quand sonner, quoi afficher) → `ReminderPlan` ; `SubscriptionReminderScheduler` ; port `IReminderService`. |
| `Security/` | `PinPolicy` (4 à 8 chiffres) ; ports `IAppLockService`, `IBiometricService`, `ISecretStore` (clé de la base). |
| `Settings/` | Port `ISettingsService` ; `ThemeOptions` et `AfterSaveOptions` (valeurs **enregistrées telles quelles** dans les préférences). |
| `Startup/` | `StartupService` : après le déverrouillage, ouvre la base, rattrape les échéances, reprogramme les rappels. |
| `Backup/` | `FinanceBackupSnapshot` et ses enregistrements (le contenu d'une sauvegarde) ; port `IDataBackupService`. |
| `Export/` | Port `IExportService`. |
| `Receipts/` | `ReceiptParser` (texte OCR d'un ticket → montant, date, commerçant) ; port `IOcrService`. |
| `Persistence/` | `IDatabaseInitializer`, `IFinanceDataStore` (instantané, restauration, remise à zéro). |
| `Abstractions/` | Ports de plateforme : `ISecureValueStore` (coffre clé/valeur), `IFileExchange` (partager ou choisir un fichier). |
| racine | `ApplicationServiceCollectionExtensions.AddApplication()` : enregistre les services en singleton. |

### Ports et qui les implémente

- **Persistance** (un port par agrégat, implémentés dans `Infrastructure/Persistence`) : `IExpenseRepository`, `ICategoryRepository`, `IIncomeRepository`, `IRecurringIncomeRepository`, `ISubscriptionRepository`, `ISavingsGoalRepository`, `IFinancialProfileRepository`, `IDueOperationsStore`, `IFinanceDataStore`, `IDatabaseInitializer`. Les suppressions sont logiques ; les lectures ne renvoient jamais une entité supprimée.
- **Sécurité, sauvegarde, export** (implémentés dans `Infrastructure`, indépendamment de la plateforme) : `ISecretStore`, `IAppLockService`, `IDataBackupService`, `IExportService`.
- **Plateforme** (implémentés dans `Maui`) : `ISecureValueStore`, `IFileExchange`, `ISettingsService`, `IBiometricService`, `IOcrService`, `IReminderService`.

### `ReceiptParser`

Cherche le total en partant de la fin du ticket (libellés « total ttc », « total », « à payer », « net à payer »), sinon le dernier montant trouvé ; accepte `1 234,56`, `12.50`, avec ou sans `€`/`EUR`, entre 0 et 100 000 ; date aux formats `dd/MM/yyyy`, `d/M/yyyy`, `dd-MM-yyyy`, `d-M-yyyy` ; commerçant = première ligne sans montant, sans date et non « bruitée ». Le résultat sert uniquement à **préremplir** la saisie : l'utilisateur confirme toujours.

### Règles de calcul à connaître

- **Solde actuel** = solde initial + **tous** les revenus − **toutes** les dépenses, y compris celles datées après aujourd'hui. C'est le comportement historique, conservé tel quel et couvert par un test.
- **Prévision de fin de mois** = solde actuel + revenus récurrents − abonnements dont l'échéance tombe après aujourd'hui et au plus tard le dernier jour du mois.
- **Comparaison** : la période courante (semaine du lundi, mois, année, jusqu'à aujourd'hui) est comparée à la période précédente **entière**.
- Les totaux additionnent les montants sans convertir les devises : si la devise des réglages change, des montants de devises différentes se mélangent.

## Règles de modification

- Une règle de calcul est une fonction `static` ou une classe sans état, avec la date du jour **en paramètre** (ou un `TimeProvider`), jamais `DateTime.Now`/`Today`. Elle vient avec un test dans `Finance.Android17.Tests`.
- Ajouter une opération de stockage : l'ajouter au port de l'agrégat concerné (dossier correspondant), l'implémenter dans `Infrastructure/Persistence`, puis l'appeler depuis un ViewModel ou un service d'ici. Pas de façade de pass-through : un consommateur dépend directement des ports dont il a besoin.
- Rien de lié à MAUI, à Android ou à EF Core.
- Montants en `long` (centimes), dates sans heure (`.Date`), `CancellationToken` sur les opérations asynchrones.
- `FinanceBackupSnapshot` fait partie du format de fichier `.mfbackup` : ne pas renommer ses propriétés. Les données ajoutées après la version 1 sont facultatives pour relire les anciens fichiers. Un changement de contenu = nouveau `FormatVersion` et adaptation de `IFinanceDataStore.RestoreSnapshotAsync` (formats 1 et 2 actuellement).
- Les valeurs de `ThemeOptions` et `AfterSaveOptions` sont persistées dans les préférences existantes : ne jamais en changer le texte.
- Conserver la séparation `IOcrService` (natif) / `ReceiptParser` (pur).
