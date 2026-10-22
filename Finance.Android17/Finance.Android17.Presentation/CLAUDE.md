# Finance.Android17.Presentation

Couche **présentation** : les ViewModels MVVM et les modèles d'affichage consommés par les pages XAML de `Finance.Android17.Maui`. Bibliothèque `net10.0`, **sans dépendance à MAUI** : elle référence `Finance.Android17.Application`, `Finance.Android17.Domain` et les NuGet `CommunityToolkit.Mvvm` (générateurs `[ObservableProperty]` et `[RelayCommand]`) et `Microsoft.Extensions.DependencyInjection.Abstractions`.

Un ViewModel **charge, appelle une règle d'`Application`, formate et réagit aux commandes** ; il ne calcule rien lui-même. Les calculs (périodes, totaux, filtres, tris, budgets, lecture des montants) sont dans `Application` ; l'affichage (textes, couleurs) est ici.

## Contenu

| Fichier | Rôle |
| --- | --- |
| `LockViewModel.cs`, `DashboardViewModel.cs`, `ExpensesViewModel.cs`, `AddExpenseViewModel.cs`, `IncomeViewModel.cs`, `SubscriptionsViewModel.cs`, `SettingsViewModel.cs` | Un ViewModel par fichier, un par page (voir ci-dessous). |
| `Rows.cs` | Modèles d'affichage : `ExpenseRow`, `ChartBar`, `SubscriptionRow`, `ActiveFilterChip`, `IncomeRow`, `RecurringIncomeRow`, `SavingsGoalRow`, `OperationRow`, `CategoryItem`, `CategoryFilterOption`, `ExpenseCategoryOption`. |
| `Formatting/MoneyFormatter.cs` | Format français des montants (`1 234,50 €`), pourcentages et dates (`02 oct. 2026`), `Hidden` (`••••`). Toujours `fr-FR`, quelle que soit la langue de l'appareil. |
| `Formatting/Palette.cs` | Couleurs d'état (budget, comparaison, revenu/dépense, sélection) en `string` hexadécimale bindable. |
| `BillingFrequency.cs` | Mensuel / Trimestriel / Annuel ↔ 1 / 3 / 12 mois : une seule table pour abonnements et revenus récurrents. |
| `PresentationServiceCollectionExtensions.cs` | `AddPresentation()` : enregistre les ViewModels en **transient**. |

### ViewModels

Tous sont des `partial class … : ObservableObject` construits par injection de dépendances. Ils ne reçoivent que des **ports** et services d'`Application` (dépôts, `ISettingsService`, `TimeProvider`…), jamais l'entité d'un autre agrégat ni une API de plateforme.

| ViewModel | Page | Responsabilité |
| --- | --- | --- |
| `LockViewModel` | `LockPage` | Création du PIN au premier lancement, vérification, biométrie facultative. Après déverrouillage, `StartupService.PrepareAsync` (base, échéances, rappels) puis événement `Unlocked` que `App` écoute. |
| `DashboardViewModel` | `DashboardPage` | Affiche le `DashboardReport` de `DashboardService` : période semaine/mois/année, total et comparaison, solde, revenus, net, prévision, taux d'épargne, catégories avec budget, budget mensuel, dépenses récentes, prochain abonnement. |
| `ExpensesViewModel` | `ExpensesPage` | Charge les dépenses puis applique `ExpenseSearch` (recherche retardée par un `CancellationTokenSource`, catégories, période, montants, six tris), puces de filtres actifs, suppression avec annulation. Lève `EditRequested` avec l'identifiant de la dépense. |
| `AddExpenseViewModel` | `AddExpensePage` | Saisie/modification d'une dépense : `AmountParser`, catégories triées par `CategoryChoice`, raccourcis de date, préremplissage depuis un ticket (`IOcrService` puis `ReceiptParser`), comportement après enregistrement. `BeginEditAsync(Guid)`. Lève `Saved`. |
| `IncomeViewModel` | `IncomePage` | Revenus (ponctuels et récurrents, un seul formulaire avec l'option « Se répète »), objectifs d'épargne et indicateurs du mois (`FinancialSummaryCalculator`, plus les revenus récurrents encore attendus). Le solde initial se règle dans `SettingsViewModel` (Réglages > Mes données). |
| `SubscriptionsViewModel` | `SubscriptionsPage` | Abonnements, totaux mensuel et annuel (`SubscriptionCosts`), rappels. |
| `SettingsViewModel` | `SettingsPage` | Thème, devise, confidentialité, budget mensuel, catégories (couleur, budget), verrouillage, biométrie, changement de PIN, export CSV, sauvegarde/restauration chiffrée, remise à zéro après confirmation du PIN. |

Les lignes d'affichage portent un **identifiant** (`Guid`) et du texte déjà formaté, jamais l'entité du domaine : le ViewModel retrouve l'entité par son identifiant dans les données qu'il vient de charger.

## Règles

- Une dépendance de plus qu'un port ou service d'`Application` (ou `TimeProvider`) est un signe qu'une règle ou une API native est au mauvais endroit.
- L'heure vient de `TimeProvider` (`clock.GetLocalNow()`), jamais de `DateTime.Now`/`Today`.
- Lire un montant saisi avec `AmountParser.TryParse` ; formater avec `MoneyFormatter`. Quand `ISettingsService.HideAmounts` est actif, les montants affichés sont remplacés par `MoneyFormatter.Hidden` ; respecter ce comportement pour tout nouveau montant.
- Les valeurs de réglages persistées (thème, comportement après enregistrement) viennent de `ThemeOptions` / `AfterSaveOptions`, pas de littéraux.
- `LoadAsync` est appelée par les pages dans `OnAppearing` : garder la méthode publique et idempotente.
- Une nouvelle page = un nouveau ViewModel ici (et son enregistrement dans `AddPresentation`), une page XAML dans `Maui` avec `x:DataType` pointant sur le ViewModel, et son enregistrement dans `MauiProgram`.
- Les icônes référencées par nom (`icon_visibility.png`, …) correspondent aux images générées depuis `Finance.Android17.Maui/Resources/Images/*.svg`.
- Texte d'interface en français. Les `internal` (`ExpenseRow.From`, `CategoryItem.From`) ne sont pas visibles des tests : on les teste à travers les ViewModels.
