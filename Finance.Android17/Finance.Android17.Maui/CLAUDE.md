# Finance.Android17.Maui

**Application .NET MAUI** « Mes Finances » : interface XAML, composition des dépendances, services natifs Android et ressources. C'est la racine de composition de la solution et le seul projet exécutable (`OutputType` `Exe`, `SingleProject`).

- Cibles : `net10.0-android` (cible réelle, minimum API 21) et `net10.0-windows10.0.19041.0` (`WindowsPackageType=None`). Les dossiers `Platforms/iOS` et `Platforms/MacCatalyst` sont des restes de template, non ciblés par le `.csproj`.
- Identité : `ApplicationTitle` « Mes Finances », `ApplicationId` `fr.nicol.finance.android17`, `ApplicationDisplayVersion` `2.0.0`, `ApplicationVersion` `8` (à garder synchronisés avec `README.md`).
- `MauiXamlInflator=SourceGen` : le XAML est compilé en C#, les pages utilisent `x:DataType` (bindings compilés).
- Références : `Finance.Android17.Application`, `Finance.Android17.Infrastructure`, `Finance.Android17.Presentation` (les ViewModels des pages). NuGet : `Microsoft.Maui.Controls`, `Microsoft.Extensions.Logging.Debug`, et `Xamarin.Google.MLKit.TextRecognition` (Android uniquement).
- Debug Android : assemblies embarquées dans l'APK, sans Fast Deployment ni shared runtime. Release Android : `android-arm64`, APK, pas de trimming ni d'AOT.

## Démarrage et navigation

1. `Platforms/Android/MainApplication` appelle `MauiProgram.CreateMauiApp()`.
2. `MauiProgram` enregistre les services métier en **singleton** (`AppSecurityService` exposé comme `IAppLockService` et `ISecretStore`, `EncryptedFinanceStore` comme `IFinanceStore`, `FinanceService`, `AppSettingsService` comme `ISettingsService`, `BiometricService`, `CsvExportService`, `OcrService`, `EncryptedBackupService`, `ReminderService`) et les ViewModels/pages/`AppShell` en **transient**.
3. `App` démarre sur `LockPage`. Après déverrouillage (PIN ou biométrie), `App.ShowMain()` remplace `Window.Page` par `AppShell`. Après `AutoLockMinutes` d'inactivité (mesurée entre `Deactivated` et `Activated`) ou sur demande (`ShowLock()`), l'écran de verrouillage revient.
4. `AppShell` construit un `TabBar` de cinq onglets : **Accueil** (`DashboardPage`), **Dépenses** (`ExpensesPage`), **Finances** (`IncomePage`), **Abonnements** (`SubscriptionsPage`), **Réglages** (`SettingsPage`). `AddExpensePage` s'ouvre en modal depuis l'accueil et la liste des dépenses.

Règle importante : ne jamais réutiliser une page MAUI après un remplacement de `Window.Page` (le contexte MAUI Android est détruit) ; pages et ViewModels restent **transient** et sont résolus via `IServiceProvider`.

## Contenu

- **Pages** (`*Page.xaml` + `*.xaml.cs`) : `LockPage`, `DashboardPage`, `ExpensesPage`, `IncomePage`, `SubscriptionsPage`, `SettingsPage`, `AddExpensePage`. Le code-behind reste minimal : liaison au ViewModel de `Presentation`, chargement dans `OnAppearing`, navigation, boîtes de dialogue de confirmation, quelques réglages d'interface natifs. La logique métier n'y a pas sa place.
- **Services natifs** (implémentent les contrats d'`Application`) :
  - `AppSecurityService` : PIN dérivé PBKDF2-SHA256 (100 000 itérations, sel aléatoire, calcul hors thread UI, comparaison à temps constant) et clé SQLCipher de 32 octets, le tout dans `SecureStorage`.
  - `AppSettingsService` : réglages simples dans `Preferences` (thème, devise, budget mensuel, verrouillage automatique, rappels, catégorie par défaut, etc.) ; le changement de thème est appliqué immédiatement.
  - `BiometricService` : actif sous Android (API 29+), indisponible ailleurs.
  - `ReminderService` : notifications d'échéances d'abonnement via `AlarmManager`, canal `subscription-reminders`, permission `POST_NOTIFICATIONS` sur Android 13+ ; `SubscriptionReminderReceiver` affiche la notification.
  - `CsvExportService` : CSV séparé par `;`, UTF-8 avec BOM, partagé par la feuille de partage native.
  - `EncryptedBackupService` : fichiers `.mfbackup` (magic `MFBAK01`, sel 16 o, nonce 12 o, tag 16 o, AES-GCM, clé PBKDF2-SHA256 200 000 itérations, mot de passe de 8 caractères minimum).
  - `OcrService` : ML Kit Text Recognition (modèle Latin embarqué) sous Android ; renvoie `null` ailleurs. Le bouton « Scanner un ticket » de `AddExpensePage.xaml` est actuellement masqué (`IsVisible="False"`).
- **Utilitaires d'interface** : `SystemBarAppearance` (couleurs et icônes des barres système Android, dont Android 15+), `ColorContrastConverter` (texte noir/blanc selon la luminance d'une couleur de catégorie).
- **`Platforms/`** : `Android` (manifeste, `MainActivity`, `MainApplication`, `CameraCapture`, `file_paths.xml`, couleurs), `Windows` (WinUI, manifeste), `iOS` et `MacCatalyst` (template inutilisé).
- **`Resources/`** : icône et splash (`#6750A4`), polices Open Sans, icônes SVG (`icon_*`, `tab_*`), `Styles/Colors.xaml` (palette) et `Styles/Styles.xaml` (styles implicites).

## Android

Permissions déclarées : `USE_BIOMETRIC`, `CAMERA`, `POST_NOTIFICATIONS`. **Aucune permission Internet**, `usesCleartextTraffic=false`, `allowBackup=false` (pour ne pas extraire la base chiffrée hors de son contexte de clé). Un `FileProvider` expose le cache pour la capture photo. Ne pas ajouter de permissions ni de réseau sans nécessité : l'application est conçue hors ligne.

## Règles de modification

- Toute nouvelle page ou ViewModel doit être enregistrée dans `MauiProgram` (transient).
- Respecter le langage visuel (Material 3 approximatif) décrit dans `../AGENTS.md` : styles nommés de `App.xaml` (`AppPage`, `PageTitle`, `Card`, `InputField`/`InputEntry`, `PrimaryButton`, `SecondaryButton`, `TextButton`, `DangerButton`, `SegmentButton`, `Fab`, `ExtendedFab`…), `Border` et non `Frame`, couleurs via `AppThemeBinding` et `Colors.xaml`, police Open Sans.
- Ne pas ajouter de convertisseur XAML générique sans vérifier les bindings existants (`ColorContrastConverter` est le seul convertisseur du projet).
- Code propre à une plateforme : `#if ANDROID` ou dossier `Platforms/` ; garder un comportement de repli sur les autres cibles.
- Opérations persistantes via les ViewModels et `FinanceService`, jamais directement sur la base depuis une page.
- Le texte de l'interface est en français ; fournir des `SemanticProperties.Description` sur les contrôles interactifs, comme dans les pages existantes.
- Si `ApplicationDisplayVersion`/`ApplicationVersion` changent, mettre aussi `README.md` à jour.
