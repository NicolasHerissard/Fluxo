# Finance.Android17.Domain

Couche **domaine** : les entités persistées de l'application. Bibliothèque `net10.0`, sans aucune dépendance (ni projet, ni NuGet). Tous les autres projets en dépendent, directement ou non.

## Contenu

| Fichier | Rôle |
| --- | --- |
| `Entity.cs` | Classe de base abstraite : `Id` (`Guid`), `CreatedAtUtc`, `UpdatedAtUtc`, `DeletedAtUtc` (suppression logique), `Version` (`Guid` régénéré à chaque modification). |
| `Expense.cs` | Dépense : montant, devise, description, date, `CategoryId` + navigation `Category`. |
| `Income.cs` | Revenu ponctuel ; `RecurringIncomeId` renseigné quand il a été généré par un revenu récurrent. |
| `RecurringIncome.cs` | Revenu récurrent : `IntervalMonths` (1, 3, 12…), `NextOccurrenceDate`, `MonthlyEquivalentMinor`. |
| `Subscription.cs` | Abonnement : `BillingIntervalMonths`, `NextBillingDate`, `ReminderEnabled`, `MonthlyEquivalentMinor`. |
| `SavingsGoal.cs` | Objectif d'épargne : cible, montant déjà épargné, date cible facultative, `Progress` (0 à 1). |
| `Category.cs` | Catégorie : nom, couleur hexadécimale (`#6750A4` par défaut), budget mensuel facultatif `BudgetMinor`. |
| `FinancialProfile.cs` | Profil mono-solde : une seule ligne (`SingletonId = 1`) qui porte `InitialBalanceMinor`. Ce n'est **pas** une `Entity` (pas de suppression logique). |
| `Class1.cs` | Placeholder de template, sans logique. |

## Règles

- **Montants en centimes** : `long AmountMinor` (et `TargetAmountMinor`, `SavedAmountMinor`, `BudgetMinor`, `InitialBalanceMinor`). Les propriétés `Amount` (`decimal`) sont des vues d'affichage calculées, non stockées. Ne jamais persister de `decimal`.
- **Devise** : `CurrencyCode`, `"EUR"` par défaut.
- **Suppression logique** : tout ce qui hérite de `Entity` se supprime en renseignant `DeletedAtUtc`. Le filtrage des supprimés est fait par la couche d'infrastructure, pas ici.
- **Propriétés calculées** (`Amount`, `MonthlyEquivalentMinor`, `Progress`) : elles sont exclues du modèle EF par `Ignore(...)` dans `Finance.Android17.Infrastructure/FinanceDbContext.cs`. Toute nouvelle propriété calculée doit y être ignorée aussi.
- **Ajouter ou modifier une propriété persistée** a un impact sur le schéma SQLCipher existant (pas de migrations EF) : voir `Finance.Android17.Infrastructure/CLAUDE.md` avant de le faire, ainsi que le DTO de sauvegarde `Contracts.cs` dans `Application`.
- Pas de logique d'accès aux données, d'UI ni de dépendance plateforme dans ce projet.
