# Finance.Android17.Maui

**Application .NET MAUI** « Mes Finances » : interface XAML, fins adaptateurs vers la plateforme et ressources. C'est la racine de composition de la solution et le seul projet exécutable (`OutputType` `Exe`, `SingleProject`). Il ne contient ni règle métier ni persistance : tout ce qui ne dépend pas de la plateforme est dans `Application`, `Infrastructure` et `Presentation`.

- Cibles : `net10.0-android` (cible réelle, minimum API 21) et `net10.0-windows10.0.19041.0` (`WindowsPackageType=None`). Les dossiers `Platforms/iOS` et `Platforms/MacCatalyst` sont des restes de template, non ciblés par le `.csproj`.
- Identité : `ApplicationTitle` « Mes Finances », `ApplicationId` `fr.nicol.finance.android17`, `ApplicationDisplayVersion` `2.0.0`, `ApplicationVersion` `8` (à garder synchronisés avec `README.md`).
- `MauiXamlInflator=SourceGen` : le XAML est compilé en C#, les pages utilisent `x:DataType` (bindings compilés).
- Références : `Finance.Android17.Application` (ports), `Finance.Android17.Infrastructure` (`AddInfrastructure`) et `Finance.Android17.Presentation` (les ViewModels des pages et `AddPresentation`). NuGet : `Microsoft.Maui.Controls`, `Microsoft.Extensions.Logging.Debug`, et `Xamarin.Google.MLKit.TextRecognition` (Android uniquement).
- Debug Android : assemblies embarquées dans l'APK, sans Fast Deployment ni shared runtime. Release Android : `android-arm64`, APK, pas de trimming ni d'AOT.

## Démarrage et navigation

1. `Platforms/Android/MainApplication` appelle `MauiProgram.CreateMauiApp()`.
2. `MauiProgram` compose l'application en quatre appels : `AddPlatformServices()` (les adaptateurs de ce projet, en **singleton**), puis `AddInfrastructure()`, `AddApplication()` et `AddPresentation()` (les ViewModels, en **transient**), puis `AddPages()` (pages et `AppShell`, en **transient**). Les deux méthodes privées `AddPlatformServices` et `AddPages` sont dans `MauiProgram`.
3. `App` démarre sur `LockPage`. Après déverrouillage (PIN ou biométrie), `App.ShowMain()` remplace `Window.Page` par `AppShell`. Après `AutoLockMinutes` d'inactivité (mesurée entre `Deactivated` et `Activated`) ou sur demande (`ShowLock()`), l'écran de verrouillage revient.
4. `AppShell` construit un `TabBar` de cinq onglets : **Accueil** (`DashboardPage`), **Dépenses** (`ExpensesPage`), **Revenus** (`IncomePage`, titre « Revenus & épargne »), **Abonnements** (`SubscriptionsPage`), **Réglages** (`SettingsPage`). `AddExpensePage` s'ouvre en modal depuis l'accueil et la liste des dépenses.

Règle importante : ne jamais réutiliser une page MAUI après un remplacement de `Window.Page` (le contexte MAUI Android est détruit) ; pages et ViewModels restent **transient** et sont résolus via `IServiceProvider`.

## Contenu

- **Pages** (`*Page.xaml` + `*.xaml.cs`) : `LockPage`, `DashboardPage`, `ExpensesPage`, `IncomePage`, `SubscriptionsPage`, `SettingsPage`, `AddExpensePage`, `NotificationsPage` (centre de notifications, ouvert par la cloche de l'en-tête) et les sous-pages modales de Réglages `CategoriesSettingsPage` et `DataSettingsPage` (même `SettingsViewModel`, instance propre). Le code-behind reste minimal : liaison au ViewModel de `Presentation`, chargement dans `OnAppearing`, navigation, boîtes de dialogue de confirmation, quelques réglages d'interface natifs. La logique métier n'y a pas sa place.
- **Adaptateurs de plateforme** (implémentent les ports d'`Application`) :
  - `MauiSecureValueStore` (`ISecureValueStore`) : lit, écrit et supprime des valeurs dans `SecureStorage`. Le PIN et la clé SQLCipher sont gérés par `Infrastructure` (`PinLockService`, `DatabaseKeyProvider`) ; ce projet ne fournit que le coffre.
  - `MauiFileExchange` (`IFileExchange`) : partage d'un fichier par la feuille de partage native et sélection d'un fichier à importer. Le CSV (`CsvExportService`) et la sauvegarde `.mfbackup` (`EncryptedBackupService`) vivent dans `Infrastructure` et passent par ce port.
  - `AppSettingsService` (`ISettingsService`) : réglages simples dans `Preferences` (thème, devise, budget mensuel, verrouillage automatique, rappels, catégorie par défaut, etc.). Les valeurs par défaut et les libellés du thème viennent de `ThemeOptions` / `AfterSaveOptions` (`Application`). `ToAppTheme(string)` est l'unique conversion du libellé en `AppTheme` ; le changement de thème est appliqué immédiatement par le setter, et au démarrage par `App`.
  - `BiometricService` : actif sous Android (API 29+), indisponible ailleurs.
  - `ReminderService` : notifications d'échéances d'abonnement via `AlarmManager`, canal `subscription-reminders`, permission `POST_NOTIFICATIONS` sur Android 13+ ; `SubscriptionReminderReceiver` affiche la notification.
  - `OcrService` : ML Kit Text Recognition (modèle Latin embarqué) sous Android ; renvoie `null` ailleurs. Le bouton « Scanner un ticket » de `AddExpensePage.xaml` est actuellement masqué (`IsVisible="False"`).
- **Contrôles et utilitaires d'interface** : le dossier `Controls/` (`IconView`, `IconButton`, `IconTile`, `SegmentedControl`, `DonutChart`, `PageHeader`, `AppIcons`, `AppColors` : voir « Langage visuel »), `SystemBarAppearance` (couleurs et icônes des barres système Android, dont Android 15+), `ColorContrastConverter` (texte noir/blanc selon la luminance d'une couleur de catégorie).
- **`Platforms/`** : `Android` (manifeste, `MainActivity`, `MainApplication`, `CameraCapture`, `file_paths.xml`, couleurs), `Windows` (WinUI, manifeste), `iOS` et `MacCatalyst` (template inutilisé).
- **`Resources/`** : icône et splash (`#4338CA`, portefeuille blanc), polices Open Sans, icônes d'onglets SVG (`tab_*`), `Styles/Colors.xaml` (palette) et `Styles/Styles.xaml` (styles implicites).

## Android

Permissions déclarées : `USE_BIOMETRIC`, `CAMERA`, `POST_NOTIFICATIONS`. **Aucune permission Internet**, `usesCleartextTraffic=false`, `allowBackup=false` (pour ne pas extraire la base chiffrée hors de son contexte de clé). Un `FileProvider` expose le cache pour la capture photo. Ne pas ajouter de permissions ni de réseau sans nécessité : l'application est conçue hors ligne.

## Règles de modification

- Toute nouvelle page est enregistrée dans `AddPages` de `MauiProgram` (transient), son ViewModel dans `AddPresentation` (`Presentation`). Un nouvel adaptateur de plateforme implémente un port d'`Application` et s'enregistre dans `AddPlatformServices`.
- N'implémenter ici que ce qui touche vraiment la plateforme (stockage sécurisé, fichiers, préférences, biométrie, OCR, notifications). Une règle, un calcul ou un accès aux données va dans `Application` / `Infrastructure`.
- Respecter le langage visuel décrit dans la section suivante : styles nommés de `App.xaml`, `Border` et non `Frame`, couleurs via `AppThemeBinding` et `Colors.xaml`, police Open Sans.
- Ne pas ajouter de convertisseur XAML générique sans vérifier les bindings existants (`ColorContrastConverter` est le seul convertisseur du projet).
- Code propre à une plateforme : `#if ANDROID` ou dossier `Platforms/` ; garder un comportement de repli sur les autres cibles.
- Opérations persistantes via les ViewModels et les ports d'`Application`, jamais directement sur la base depuis une page.
- Le texte de l'interface est en français ; fournir des `SemanticProperties.Description` sur les contrôles interactifs, comme dans les pages existantes.
- Si `ApplicationDisplayVersion`/`ApplicationVersion` changent, mettre aussi `README.md` à jour.

## Langage visuel

Refonte issue de la maquette Figma « Gestion financière personnelle » (indigo, cartes à bord fin, titres en serif). Tout est centralisé dans `Resources/Styles/Colors.xaml` (palette), `Resources/Styles/Styles.xaml` (styles MAUI implicites), `App.xaml` (styles nommés) et `Controls/` (contrôles dessinés).

**Styles nommés de `App.xaml`** : `AppPage`, `Eyebrow` (petites capitales), `PageTitle`, `SectionTitle`, `DisplayAmount`, `BodyLabel`, `StrongLabel`, `MutedLabel`, `CaptionLabel`, `FieldLabel`, `PrimaryText`, `StatusLabel`, `ErrorLabel`, `Card`, `SoftCard`, `Pill`, `Toast` / `ToastText` / `ToastAction`, `InputField`, `InputEntry`, `PrimaryButton`, `SecondaryButton`, `TextButton`, `DangerButton`, `ChipButton`.

- **Thèmes** : chaque couleur existe en deux clés `XxxLight` / `XxxDark`, toujours associées par `AppThemeBinding` (XAML) ou par `AppColors.Bind` / `AppColors.Variant` (code). Le thème (Clair / Sombre / Système) s'applique donc partout sans recharger. Pour une nouvelle couleur, ajouter **les deux** variantes ; ne jamais coder un hexadécimal de thème dans une page. Les seules couleurs fixes (identiques dans les deux thèmes) sont `IncomeBarColor`, `ExpenseBarColor`, `Slider*Color`, `ScrimColor`, `FabShadowColor`, `White`, `Black` et celles des catégories (choisies par l'utilisateur).
- **Palette** : fond de page `#F6F5FF` / `#0B0E1A`, surface `#FFFFFF` / `#141829`, texte `#1E1B4B` / `#E8EAFB`, accent `#4338CA` / `#8B9CFF`. Les teintes d'état vont par paires « douce » + « encre » (`ExpenseSoft` / `ExpenseInk`, `WarningSoft` / `WarningInk`, `SuccessSoft` / `SuccessInk`, `VioletSoft` / `VioletInk`, `BlueSoft` / `BlueInk`) : fond de pastille et texte ou icône posés dessus.
- **Typographie** : Open Sans (`OpenSansRegular`, `OpenSansSemibold`) pour le texte ; la ressource `DisplayFont` (serif système : `serif` sur Android, `Georgia` sous Windows) pour les titres et les montants. Pour embarquer une vraie police d'affichage (DM Serif Display…), ajouter le fichier, l'enregistrer dans `ConfigureFonts` et changer la seule ressource `DisplayFont`. Titres de page 32, de section 19 à 21, montant principal 40, corps 14, légendes 12 à 13.
- **Espacements** : 20 de marge latérale, 16 entre blocs, 8 à 12 dans une carte ; `PageHeader` apporte 18 en haut.
- **Rayons** : `Card` 14, champs et boutons 10, pastilles d'icône 30 % de leur côté, boutons flottants ronds.
- **Cartes** : `Border` `Card` (fond `Surface`, trait `Outline`). Ne pas réintroduire `Frame`. Aucun dégradé : le résumé de solde est une carte comme les autres, avec un montant en `DisplayAmount`.
- **Ombres** : réservées au bouton flottant (`IconButton` en style `Fab`).
- **Icônes** : plus aucune image `icon_*.png`. Les icônes sont des tracés Lucide 24 × 24 dans `Controls/AppIcons.cs`, dessinés par `IconView` et teintés selon le thème. Pour en ajouter une, ajouter son tracé à `AppIcons` (chemin SVG ; les arcs sont acceptés). Seules exceptions : les icônes de la barre d'onglets (`Resources/Images/tab_*.svg`, trait `#6B6890`, que Shell teinte) et l'icône d'application / splash.
- **Contrôles de `Controls/`** : `IconView` (icône), `IconButton` (zone tactile ≥ 40 : styles `Plain`, `Small`, `Tile`, `Filled`, `Fab`, point de notification `HasBadge`), `IconTile` (pastille d'icône teintée par `Tone`), `SegmentedControl` (choix exclusif ; le choix part par `Command`, la sélection revient par `SelectedItem`), `DonutChart` (anneau de répartition), `PageHeader` (surtitre, titre, sous-titre, **cloche de notifications**, verrouillage facultatif). `AppColors` lit `Colors.xaml` depuis le code.
- **Notifications** : la cloche de `PageHeader` affiche un point tant que `NotificationsViewModel` contient une alerte non consultée ; elle ouvre `NotificationsPage` en modal, qui marque les alertes comme vues à sa fermeture. Les alertes (budgets, découvert probable, prélèvements des 7 prochains jours) sont calculées par `Application` (`NotificationBuilder`) ; ce projet n'ajoute que l'affichage.
- **États sélectionnés** : fond `Primary` et texte `OnPrimary` (puces, segments) ; pour les catégories colorées, contour clair accompagné d'une coche (voir `ExpenseCategoryOption` dans `Presentation`).
- **Champs** : entourer les `Entry`, `Picker` et `DatePicker` d'un `Border` `InputField`, avec `InputEntry` sur les champs texte. Les mappings globaux de `MauiProgram` retirent le soulignement natif Android ; ne pas les contourner.
- **Boutons** : `PrimaryButton`, `SecondaryButton`, `TextButton`, `DangerButton`, `ChipButton` ; actions d'icône par `IconButton`. Ne pas recopier couleurs et dimensions dans les pages.
- **Listes** : une ligne de liste est une `Card` dont le toucher modifie l'élément (`TapGestureRecognizer`) et dont la suppression est un `IconButton` `Small` ; garder `SemanticProperties.Hint` sur la ligne et `Description` sur chaque `IconButton` (l'icône seule ne dit rien).
- **Barres système Android** : gérées globalement par `SystemBarAppearance` (fond `#F6F5FF` ou `#0B0E1A`, couleur des icônes, y compris au changement de thème et sous Android 15+). Garder ces deux valeurs alignées sur `PageBackground`.
- **Thème** : le choix Clair / Sombre / Système (`ThemeOptions`) est appliqué immédiatement par `AppSettingsService` (et au démarrage par `App`), via `AppSettingsService.ToAppTheme`. `SettingsPage` le propose en trois segments (`SettingsViewModel.ThemeChoices`, `SelectThemeCommand`).
- **Contrôles MAUI** : privilégier ceux déjà employés (`Border`, `CollectionView`, `Grid`, `FlexLayout`, `DatePicker`, `ProgressBar`, `AppThemeBinding`, déclencheurs de données) et les bindings MVVM compilés (`x:DataType`).
