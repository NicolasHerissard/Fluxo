# Finance.Android17.Domain

Couche **domaine** : les entités persistées de l'application. Bibliothèque `net10.0`, sans aucune dépendance (ni projet, ni NuGet). Tous les autres projets en dépendent, directement ou non.

## Contenu

| Fichier | Rôle |
| --- | --- |
| `Entity.cs` | Classe de base abstraite : `Id` (`Guid`), `CreatedAtUtc`, `UpdatedAtUtc`, `DeletedAtUtc` (suppression logique), `Version` (`Guid`). Méthodes `Touch(nowUtc)` (date la mise à jour et **renouvelle `Version`**), `MarkDeleted(nowUtc)` et `MarkRestored(nowUtc)`. |
| `Expense.cs` | Dépense : montant, devise, description, date, `CategoryId` + navigation `Category`. |
| `Income.cs` | Revenu ponctuel ; `RecurringIncomeId` renseigné quand il a été généré par un revenu récurrent. |
| `RecurringIncome.cs` | Revenu récurrent : `IntervalMonths` (1, 3, 12…), `NextOccurrenceDate`, `AnchorDay` (jour du mois voulu), `MonthlyEquivalentMinor`. |
| `Subscription.cs` | Abonnement : `BillingIntervalMonths`, `NextBillingDate`, `AnchorDay` (jour du mois voulu), `ReminderEnabled`, `MonthlyEquivalentMinor`. |
| `SavingsGoal.cs` | Objectif d'épargne : cible, montant déjà épargné, date cible facultative, `Progress` (0 à 1). |
| `Category.cs` | Catégorie : nom, couleur hexadécimale (`#6750A4` par défaut), budget mensuel facultatif `BudgetMinor`. |
| `FinancialProfile.cs` | Profil mono-solde : une seule ligne (`SingletonId = 1`) qui porte `InitialBalanceMinor`. Ce n'est **pas** une `Entity` (pas de suppression logique). |
| `DefaultCategories.cs` | Les cinq catégories fournies (Alimentation, Transport, Logement, Loisirs, Autre) et la catégorie « Abonnements » créée à la première échéance : une seule définition pour tout le projet. |

## Règles

- **Montants en centimes** : `long AmountMinor` (et `TargetAmountMinor`, `SavedAmountMinor`, `BudgetMinor`, `InitialBalanceMinor`). Les propriétés `Amount` (`decimal`) sont des vues d'affichage calculées, non stockées. Ne jamais persister de `decimal`.
- **Devise** : `CurrencyCode`, `"EUR"` par défaut.
- **Suppression logique** : tout ce qui hérite de `Entity` se supprime avec `MarkDeleted` (qui renseigne `DeletedAtUtc`). Le filtrage des supprimés est fait par les dépôts d'`Infrastructure`, pas ici.
- **Horodatage** : les dates de création, modification et suppression viennent de l'horloge injectée dans les dépôts (`Touch`, `MarkDeleted`, `MarkRestored` prennent l'instant en paramètre). Les valeurs par défaut de `DateTime.UtcNow` / `DateTime.Today` des propriétés ne servent que de repli à l'instanciation ; les dépôts les écrasent.
- **Propriétés calculées** (`Amount`, `MonthlyEquivalentMinor`, `Progress`) : elles sont exclues du modèle EF par `Ignore(...)` dans `Finance.Android17.Infrastructure/Persistence/FinanceDbContext.cs`. Toute nouvelle propriété calculée doit y être ignorée aussi.
- **Ajouter ou modifier une propriété persistée** change le schéma SQLCipher existant : il faut une migration dans `FinanceSchema` (voir `Finance.Android17.Infrastructure/CLAUDE.md`) et la prise en compte dans `FinanceBackupSnapshot` (`Application/Backup`).
- Pas de logique d'accès aux données, d'UI ni de dépendance plateforme dans ce projet.
