# Finance.Android17.Presentation

Couche **présentation** : les ViewModels MVVM et les modèles d'affichage consommés par les pages XAML de `Finance.Android17.Maui`. Bibliothèque `net10.0`, **sans dépendance à MAUI** : elle référence `Finance.Android17.Application`, `Finance.Android17.Domain` et le NuGet `CommunityToolkit.Mvvm` (générateurs `[ObservableProperty]` et `[RelayCommand]`).

## Contenu

| Fichier | Rôle |
| --- | --- |
| `AppViewModels.cs` | Modèles d'affichage et ViewModels de l'accueil, des dépenses, des abonnements et des réglages (~1 000 lignes). |
| `IncomeViewModel.cs` | `IncomeViewModel` (onglet « Finances ») et `SavingsGoalRow`. |
| `Class1.cs` | Placeholder de template, sans logique. |

### ViewModels

Tous sont des `partial class ... : ObservableObject` construits par injection de dépendances (enregistrés en transient dans `MauiProgram`). Chacun correspond à une page MAUI.

| ViewModel | Page | Responsabilité |
| --- | --- | --- |
| `LockViewModel` | `LockPage` | Création du PIN au premier lancement (4 à 8 chiffres), vérification, biométrie facultative. `PrepareFinanceAsync` initialise la base, traite les abonnements et revenus récurrents échus (selon les réglages) et planifie les rappels, puis lève l'événement `Unlocked` que `App` écoute. |
| `DashboardViewModel` | `DashboardPage` | Périodes semaine/mois/année (`GetRanges`), total et comparaison avec la période précédente, solde, revenus, résultat net, prévision de fin de mois et taux d'épargne (via `FinanceService.BuildFinancialSummary`), trois catégories principales avec budget, budget mensuel global, trois dépenses récentes, prochain abonnement. |
| `ExpensesViewModel` | `ExpensesPage` | Charge toutes les dépenses puis filtre et trie **côté application** : recherche normalisée (retardée par un `CancellationTokenSource`), catégories, période, montants min/max, six tris, puces de filtres actifs, suppression avec annulation (`UndoDeleteAsync`). Lève `EditRequested` pour ouvrir l'édition. |
| `AddExpenseViewModel` | `AddExpensePage` | Saisie/modification d'une dépense : validation du montant, catégories triées par usage récent, raccourcis de date, préremplissage depuis un ticket (`ApplyReceiptAsync` : `IOcrService` puis `ReceiptParser`), comportement après enregistrement (« Fermer » / « Ajouter une autre »). Lève `Saved`. |
| `IncomeViewModel` | `IncomePage` | Solde initial, revenus ponctuels, revenus récurrents (mensuel/trimestriel/annuel), objectifs d'épargne et indicateurs mono-solde. |
| `SubscriptionsViewModel` | `SubscriptionsPage` | Abonnements mensuels/trimestriels/annuels, totaux mensuel et annuel, rappels. |
| `SettingsViewModel` | `SettingsPage` | Thème, devise, confidentialité des montants, budget mensuel, catégories (couleur, budget), verrouillage automatique, biométrie, changement de PIN, export CSV, sauvegarde/restauration chiffrée, remise à zéro après confirmation du PIN. |

Modèles d'affichage : `ExpenseRow`, `ChartBar`, `SubscriptionRow`, `ActiveFilterChip`, `SavingsGoalRow`, `CategoryFilterOption`, `ExpenseCategoryOption`. Ils portent déjà du texte formaté (montants, dates `fr-FR`) et, pour certains, des couleurs hexadécimales sous forme de `string`, directement bindables en XAML.

## Règles

- Les ViewModels ne dépendent que des contrats de `Application` (`FinanceService`, `ISettingsService`, `IAppLockService`, `IBiometricService`, `IOcrService`, `IExportService`, `IDataBackupService`, `IReminderService`) : aucun accès direct à la base ni API MAUI/Android ici. Ce qui est natif passe par une interface d'`Application` implémentée dans `Maui`.
- Convertir les saisies en centimes avec `AddExpenseViewModel.TryAmount` (`MidpointRounding.AwayFromZero`, virgule ou point accepté) ; formater avec `DashboardViewModel.Money` / `CurrencySymbol`. Ces helpers sont `internal` et partagés entre les ViewModels du projet.
- Quand `ISettingsService.HideAmounts` est actif, les montants affichés sont remplacés par `••••`; les données restent stockées normalement. Respecter ce comportement pour tout nouveau montant affiché.
- Les opérations asynchrones de chargement (`LoadAsync`) sont appelées par les pages dans `OnAppearing` ; garder la méthode publique et idempotente.
- Les icônes référencées par nom (`icon_visibility.png`, `icon_visibility_off.png`) correspondent aux images générées depuis `Finance.Android17.Maui/Resources/Images/*.svg`.
- Une nouvelle page = un nouveau ViewModel ici, une page XAML dans `Maui` (`x:DataType` pointant sur le ViewModel) et leur enregistrement transient dans `MauiProgram`.
- Texte d'interface en français. Ne pas déplacer ici de règle métier réutilisable : elle appartient à `Application` (et doit être testée dans `Finance.Android17.Tests`).
