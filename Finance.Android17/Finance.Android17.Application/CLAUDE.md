# Finance.Android17.Application

Couche **application** : les contrats que le reste de la solution implémente ou consomme, la façade `FinanceService` et les règles de calcul indépendantes de l'interface. Bibliothèque `net10.0`, référence uniquement `Finance.Android17.Domain` (aucun NuGet).

## Contenu

| Fichier | Rôle |
| --- | --- |
| `Contracts.cs` | Toutes les abstractions et DTO (voir ci-dessous). |
| `FinanceService.cs` | `FinanceService(IFinanceStore store)` : façade mince sur le stockage + trois fonctions statiques pures. |
| `ReceiptParser.cs` | Analyse du texte OCR d'un ticket : `ReceiptParser.Parse(text)` → `ReceiptDraft(AmountMinor, OccurredOn, MerchantName, RawText)`. |
| `Class1.cs` | Placeholder de template, sans logique. |

### Contrats (`Contracts.cs`)

- `IFinanceStore` : toute la persistance (catégories, dépenses, revenus, revenus récurrents, objectifs, abonnements, solde initial, traitement des échéances, snapshots de sauvegarde, remise à zéro). Implémentée par `EncryptedFinanceStore` dans `Infrastructure`.
- Services natifs implémentés par `Finance.Android17.Maui` : `ISecretStore` (clé de base), `IAppLockService` (PIN), `IBiometricService`, `ISettingsService` (réglages persistés), `IExportService` (CSV), `IOcrService` (+ `OcrTextResult`), `IDataBackupService` (sauvegarde chiffrée), `IReminderService`.
- `ExpenseFilter(Search, CategoryId, From, To)` : filtre de lecture des dépenses.
- DTO de sauvegarde : `FinanceBackupSnapshot` (format **2**) et ses enregistrements `CategoryBackup`, `ExpenseBackup`, `SubscriptionBackup`, `IncomeBackup`, `RecurringIncomeBackup`, `SavingsGoalBackup`. Les listes ajoutées en format 2 sont nullables pour pouvoir relire le format 1.
- Résultats calculés : `DashboardSummary`, `FinancialSummary`.

### Règles pures de `FinanceService`

- `BuildDashboard(expenses, today)` : total du mois courant, nombre de dépenses, catégorie principale (« Sans catégorie » / « — » à défaut).
- `GetDueDates(nextBillingDate, intervalMonths, today)` : toutes les échéances manquées jusqu'à aujourd'hui inclus (intervalle minimum 1 mois). Utilisée par `Infrastructure` pour générer dépenses d'abonnement et revenus récurrents, et par les tests.
- `BuildFinancialSummary(...)` : dépenses/revenus du mois, résultat net, solde actuel (solde initial + tous les revenus − toutes les dépenses), solde prévisionnel de fin de mois (échéances d'abonnements et de revenus récurrents restant dans le mois), taux d'épargne borné à [-1 ; 1].

### `ReceiptParser`

Cherche le total en partant de la fin du ticket (libellés « total ttc », « total », « à payer », « net à payer »), sinon le dernier montant trouvé ; accepte `1 234,56`, `12.50`, avec ou sans `€`/`EUR`, entre 0 et 100 000 ; date aux formats `dd/MM/yyyy`, `d/M/yyyy`, `dd-MM-yyyy`, `d-M-yyyy` ; commerçant = première ligne sans montant, sans date et non « bruitée ». Le résultat sert uniquement à **préremplir** la saisie : l'utilisateur confirme toujours.

## Règles de modification

- Ajouter une opération de stockage : `IFinanceStore` → implémentation dans `Infrastructure` → exposition par `FinanceService` → utilisation dans un ViewModel.
- Garder ici les règles métier et abstractions réutilisables ; rien de lié à MAUI, à Android ou à EF Core.
- Montants en `long` (centimes), dates sans heure (`.Date`), `CancellationToken` sur les opérations asynchrones.
- Toute modification de `FinanceBackupSnapshot` doit rester compatible avec les fichiers `.mfbackup` existants (formats 1 et 2) : incrémenter `FormatVersion` et adapter `RestoreBackupSnapshotAsync` plutôt que de casser le format.
- Conserver la séparation `IOcrService` / `ReceiptParser`.
- Les règles pures (`BuildDashboard`, `GetDueDates`, `BuildFinancialSummary`, `ReceiptParser`) sont celles qui doivent être couvertes par `Finance.Android17.Tests`.
