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
4. `AppShell` construit un `TabBar` de cinq onglets : **Accueil** (`DashboardPage`), **Dépenses** (`ExpensesPage`), **Finances** (`IncomePage`), **Abonnements** (`SubscriptionsPage`), **Réglages** (`SettingsPage`). `AddExpensePage` s'ouvre en modal depuis l'accueil et la liste des dépenses.

Règle importante : ne jamais réutiliser une page MAUI après un remplacement de `Window.Page` (le contexte MAUI Android est détruit) ; pages et ViewModels restent **transient** et sont résolus via `IServiceProvider`.

## Contenu

- **Pages** (`*Page.xaml` + `*.xaml.cs`) : `LockPage`, `DashboardPage`, `ExpensesPage`, `IncomePage`, `SubscriptionsPage`, `SettingsPage`, `AddExpensePage`, et les sous-pages modales de Réglages `CategoriesSettingsPage` et `DataSettingsPage` (même `SettingsViewModel`, instance propre). Le code-behind reste minimal : liaison au ViewModel de `Presentation`, chargement dans `OnAppearing`, navigation, boîtes de dialogue de confirmation, quelques réglages d'interface natifs. La logique métier n'y a pas sa place.
- **Adaptateurs de plateforme** (implémentent les ports d'`Application`) :
  - `MauiSecureValueStore` (`ISecureValueStore`) : lit, écrit et supprime des valeurs dans `SecureStorage`. Le PIN et la clé SQLCipher sont gérés par `Infrastructure` (`PinLockService`, `DatabaseKeyProvider`) ; ce projet ne fournit que le coffre.
  - `MauiFileExchange` (`IFileExchange`) : partage d'un fichier par la feuille de partage native et sélection d'un fichier à importer. Le CSV (`CsvExportService`) et la sauvegarde `.mfbackup` (`EncryptedBackupService`) vivent dans `Infrastructure` et passent par ce port.
  - `AppSettingsService` (`ISettingsService`) : réglages simples dans `Preferences` (thème, devise, budget mensuel, verrouillage automatique, rappels, catégorie par défaut, etc.). Les valeurs par défaut et les libellés du thème viennent de `ThemeOptions` / `AfterSaveOptions` (`Application`). `ToAppTheme(string)` est l'unique conversion du libellé en `AppTheme` ; le changement de thème est appliqué immédiatement par le setter, et au démarrage par `App`.
  - `BiometricService` : actif sous Android (API 29+), indisponible ailleurs.
  - `ReminderService` : notifications d'échéances d'abonnement via `AlarmManager`, canal `subscription-reminders`, permission `POST_NOTIFICATIONS` sur Android 13+ ; `SubscriptionReminderReceiver` affiche la notification.
  - `OcrService` : ML Kit Text Recognition (modèle Latin embarqué) sous Android ; renvoie `null` ailleurs. Le bouton « Scanner un ticket » de `AddExpensePage.xaml` est actuellement masqué (`IsVisible="False"`).
- **Utilitaires d'interface** : `SystemBarAppearance` (couleurs et icônes des barres système Android, dont Android 15+), `ColorContrastConverter` (texte noir/blanc selon la luminance d'une couleur de catégorie).
- **`Platforms/`** : `Android` (manifeste, `MainActivity`, `MainApplication`, `CameraCapture`, `file_paths.xml`, couleurs), `Windows` (WinUI, manifeste), `iOS` et `MacCatalyst` (template inutilisé).
- **`Resources/`** : icône et splash (`#6750A4`), polices Open Sans, icônes SVG (`icon_*`, `tab_*`), `Styles/Colors.xaml` (palette) et `Styles/Styles.xaml` (styles implicites).

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

Thème proche de Material 3, centralisé dans `Resources/Styles/Colors.xaml` (palette), `Resources/Styles/Styles.xaml` (styles MAUI implicites) et `App.xaml` (styles nommés : `AppPage`, `PageTitle`, `SectionTitle`, `BodyLabel`, `MutedLabel`, `FieldLabel`, `Card`, `InputField`, `InputEntry`, `PrimaryButton`, `SecondaryButton`, `TextButton`, `DangerButton`, `SegmentButton`, `Fab`, `ExtendedFab`).

- **Couleurs** : accent principal `#6750A4`, conteneur principal clair `#EADDFF`, danger `#B3261E`, succès `#2E7D32`. Fonds de page `#F7F5FA` (clair) et `#121212` (sombre) ; texte principal `#201A2B` et `#F2EDF7` ; texte secondaire `#625B71` et `#CAC4D0`. Utiliser les clés de `Colors.xaml` avec `AppThemeBinding` plutôt que des valeurs en dur dans les pages.
- **Typographie** : police Open Sans ; titres de page à 28, titres de section à 20 (généralement), corps à 14, légendes à 13.
- **Espacements** : 8, 12, 16 et 20 reviennent partout ; les pages utilisent généralement 20 de marge latérale.
- **Rayons** : 12 à 18 pour les contrôles et cartes (`Card` 18, boutons 12 à 16, `InputField` 14), 24 pour les grands panneaux, 28 pour les boutons flottants.
- **Cartes** : `Border` avec fond blanc ou `#211F26` et trait discret `#E7E0EC` ou `#49454F`. Ne pas réintroduire `Frame`.
- **Ombres et dégradés** : les ombres sont réservées aux éléments flottants (`Fab`). Seuls les deux grands résumés de solde en tête de `DashboardPage` et d'`IncomePage` (onglet « Finances ») utilisent un dégradé (`#7658B5` vers `#51358D`) ; ne pas en ajouter ailleurs.
- **États sélectionnés** : violet principal et texte contrasté ; pour les catégories colorées, contour clair accompagné d'une coche (voir `ExpenseCategoryOption` dans `Presentation`).
- **Champs** : entourer les `Entry`, `Picker` et `DatePicker` d'un `Border` `InputField`, avec `InputEntry` sur les champs texte. Les mappings globaux de `MauiProgram` retirent le soulignement natif Android ; ne pas les contourner.
- **Boutons** : employer `PrimaryButton`, `SecondaryButton`, `TextButton`, `DangerButton`, `SegmentButton`, `Fab` ou `ExtendedFab` au lieu de recopier couleurs et dimensions dans les pages.
- **Barres système Android** : gérées globalement par `SystemBarAppearance` (fond clair ou sombre, couleur des icônes, y compris au changement de thème et sous Android 15+).
- **Thème** : le choix Clair / Sombre / Système (`ThemeOptions`) est appliqué immédiatement par `AppSettingsService` (et au démarrage par `App`), via `AppSettingsService.ToAppTheme`.
- **Contrôles** : privilégier les contrôles MAUI déjà employés (`Border`, `CollectionView`, `Grid`, `DatePicker`, `ProgressBar`, `AppThemeBinding`, états visuels) et les bindings MVVM compilés (`x:DataType`).
