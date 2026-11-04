# Finance.Android17.Presentation

Couche **présentation** : les ViewModels MVVM et les modèles d'affichage consommés par les pages XAML de `Finance.Android17.Maui`. Bibliothèque `net10.0`, **sans dépendance à MAUI** : elle référence `Finance.Android17.Application`, `Finance.Android17.Domain` et les NuGet `CommunityToolkit.Mvvm` (générateurs `[ObservableProperty]` et `[RelayCommand]`) et `Microsoft.Extensions.DependencyInjection.Abstractions`.

Un ViewModel **charge, appelle une règle d'`Application`, formate et réagit aux commandes** ; il ne calcule rien lui-même. Les calculs (périodes, totaux, filtres, tris, budgets, lecture des montants) sont dans `Application` ; l'affichage (textes, couleurs) est ici.

## Contenu

| Fichier | Rôle |
| --- | --- |
| `LockViewModel.cs`, `DashboardViewModel.cs`, `ExpensesViewModel.cs`, `AddExpenseViewModel.cs`, `IncomeViewModel.cs`, `SubscriptionsViewModel.cs`, `SettingsViewModel.cs`, `CategoriesSettingsViewModel.cs`, `DataSettingsViewModel.cs`, `NotificationsViewModel.cs` | Un ViewModel par fichier, un par page (voir ci-dessous). |
| `SavingsGoalsViewModel.cs` | Sous-ViewModel des objectifs d'épargne, exposé par `IncomeViewModel.Goals` (pas de page propre, pas enregistré dans l'injection). |
| `StatusViewModelBase.cs`, `UndoableViewModelBase.cs` | Bases communes : message d'état et erreur (`StatusMessage`, `StatusIsError`, `ShowError`, `Describe`) ; annulation des suppressions **empilée** (`OfferUndo`, `IsUndoVisible`, `UndoMessage`, `UndoDeleteCommand`, 8 s) pour `ExpensesViewModel`, `SubscriptionsViewModel` et `IncomeViewModel`. |
| `Rows.cs` | Modèles d'affichage : `ExpenseRow`, `ChartBar`, `SubscriptionRow`, `ActiveFilterChip`, `IncomeRow`, `RecurringIncomeRow`, `SavingsGoalRow`, `OperationRow`, `CategoryItem`, `CategoryFilterOption`, `ExpenseCategoryOption`, `DonutSlice` (part de l'anneau des catégories), `CashflowBar` (barres revenus / dépenses d'un mois), `NotificationRow`. |
| `NotificationTexts.cs` | Titre, message et icône d'une alerte (`AppNotification`), avec montants masqués si besoin. Partagé par `NotificationsViewModel` et par les notifications du système envoyées en arrière-plan (projet `Maui`). |
| `Formatting/MoneyFormatter.cs` | Format français des montants (`1 234,50 €`), pourcentages et dates (`02 oct. 2026`), `Hidden` (`••••`). Toujours `fr-FR`, quelle que soit la langue de l'appareil. |
| `Formatting/Palette.cs` | Couleurs **indépendantes du thème** en `string` hexadécimale bindable (état de budget, sélection, couleur de catégorie suggérée). Les couleurs de texte et de fond qui changent avec le thème ne passent pas ici : le ViewModel expose un booléen (`ComparisonIsGood`, `ForecastIsHealthy`…) et la page choisit la teinte avec `AppThemeBinding`. |
| `BillingFrequency.cs` | Mensuel / Trimestriel / Annuel ↔ 1 / 3 / 12 mois : une seule table pour abonnements et revenus récurrents. |
| `PresentationServiceCollectionExtensions.cs` | `AddPresentation()` : enregistre les ViewModels en **transient**. |

### ViewModels

Tous sont des `partial class … : ObservableObject` construits par injection de dépendances. Ils ne reçoivent que des **ports** et services d'`Application` (dépôts, `ISettingsService`, `TimeProvider`…), jamais l'entité d'un autre agrégat ni une API de plateforme.

| ViewModel | Page | Responsabilité |
| --- | --- | --- |
| `LockViewModel` | `LockPage` | Création du PIN au premier lancement, vérification, biométrie facultative. Si la clé de la base est introuvable (`DatabaseKeyUnavailableException`), `CanStartOver` affiche « Repartir d'une base neuve » : la page confirme puis appelle `StartOverAsync` (`IDatabaseRecovery`). Après déverrouillage, `StartupService.PrepareAsync` (base, échéances, rappels) puis événement `Unlocked` que `App` écoute. |
| `DashboardViewModel` | `DashboardPage` | Affiche le `DashboardReport` de `DashboardService` : période semaine/mois/année (`Periods`), total et comparaison, solde, revenus, net, prévision (`ForecastBadge`), taux d'épargne, anneau et légende des catégories (`CategorySlices`, `CategoryBars`), flux de trésorerie sur six mois (`CashflowBars`), budget mensuel, dépenses récentes, prochain abonnement. `PrivacyIcon` est un nom d'icône (`eye` / `eye-off`). |
| `NotificationsViewModel` | `NotificationsPage` et cloche de `PageHeader` | Alertes du `NotificationService` en `NotificationRow` (titre, message, icône, gravité `info` / `warning` / `critical`, `IsNew`) ; `HasUnread` allume le point de la cloche ; `MarkAllReadCommand` les marque vues. Montants masqués si `HideAmounts`. |
| `ExpensesViewModel` | `ExpensesPage` | Charge les dépenses puis applique `ExpenseSearch` (recherche retardée par un `CancellationTokenSource`, catégories, période, montants, six tris), puces de filtres actifs, suppression avec annulation. Lève `EditRequested` avec l'identifiant de la dépense. |
| `AddExpenseViewModel` | `AddExpensePage` | Saisie/modification d'une dépense : `AmountParser`, catégories triées par `CategoryChoice`, raccourcis de date, préremplissage depuis un ticket (`IOcrService` puis `ReceiptParser`), comportement après enregistrement. `BeginEditAsync(Guid)`. Lève `Saved`. |
| `IncomeViewModel` | `IncomePage` | Revenus (ponctuels et récurrents, un seul formulaire avec l'option « Se répète »), indicateurs du mois (`FinancialSummaryCalculator`, plus les revenus récurrents encore attendus). Les objectifs d'épargne sont dans `Goals` (`SavingsGoalsViewModel`, lié à la page par `BindingContext="{Binding Goals}"`) ; il lui prête annulation et rechargement. Le solde initial se règle dans `DataSettingsViewModel`. |
| `SubscriptionsViewModel` | `SubscriptionsPage` | Abonnements, totaux mensuel et annuel (`SubscriptionCosts`), rappels. |
| `SettingsViewModel` | `SettingsPage` | Thème, devise, confidentialité, budget mensuel, verrouillage, biométrie, changement de PIN, rappels, alertes, comportement après enregistrement. |
| `CategoriesSettingsViewModel` | `CategoriesSettingsPage` | Catégories : création, modification (nom, couleur RGB/hex, budget) et suppression. |
| `DataSettingsViewModel` | `DataSettingsPage` | Solde de départ, export CSV, sauvegarde et restauration chiffrées, remise à zéro après confirmation du PIN. |

Les lignes d'affichage portent un **identifiant** (`Guid`) et du texte déjà formaté, jamais l'entité du domaine : le ViewModel retrouve l'entité par son identifiant dans les données qu'il vient de charger.

## Règles

- Une dépendance de plus qu'un port ou service d'`Application` (ou `TimeProvider`) est un signe qu'une règle ou une API native est au mauvais endroit.
- L'heure vient de `TimeProvider` (`clock.GetLocalNow()`), jamais de `DateTime.Now`/`Today`.
- Lire un montant saisi avec `AmountParser.TryParse` ; formater avec `MoneyFormatter`. Quand `ISettingsService.HideAmounts` est actif, les montants affichés sont remplacés par `MoneyFormatter.Hidden` ; respecter ce comportement pour tout nouveau montant.
- Les valeurs de réglages persistées (thème, comportement après enregistrement) viennent de `ThemeOptions` / `AfterSaveOptions`, pas de littéraux.
- `LoadAsync` est appelée par les pages dans `OnAppearing` : garder la méthode publique et idempotente.
- Une nouvelle page = un nouveau ViewModel ici (et son enregistrement dans `AddPresentation`), une page XAML dans `Maui` avec `x:DataType` pointant sur le ViewModel, et son enregistrement dans `MauiProgram`.
- Les icônes référencées par nom (`eye`, `eye-off`, `calendar`, `wallet`…) sont des clés de `Finance.Android17.Maui/Controls/AppIcons.cs`, dessinées par `IconView` : une clé inconnue n'affiche rien.
- Texte d'interface en français. Les `internal` (`ExpenseRow.From`, `CategoryItem.From`) ne sont pas visibles des tests : on les teste à travers les ViewModels.
