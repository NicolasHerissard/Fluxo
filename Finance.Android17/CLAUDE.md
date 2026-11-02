# Finance.Android17 — vue d'ensemble de la solution

Application Android (et Windows) de gestion de finances personnelles, **locale, hors ligne et mono-solde** : dépenses, revenus ponctuels et récurrents, abonnements, objectifs d'épargne, solde initial. Nom affiché : « Mes Finances », identifiant Android `fr.nicol.finance.android17`, version `2.0.0` (code de version `8`). Stack : .NET 10, .NET MAUI, EF Core 10, SQLite chiffré avec SQLCipher, CommunityToolkit.Mvvm.

La cible Android est l'API 36 (la plus récente prise en charge par le SDK .NET 10 installé) ; l'application est testée sur un Google Pixel 10 sous Android 17 / API 37, Android assurant la compatibilité ascendante. La cible Windows (`net10.0-windows10.0.19041.0`) existe aussi.

Le point d'entrée de la solution est `Finance.Android17.slnx` (format `.slnx`, Visual Studio 2026 ou SDK .NET 10). Tous les dossiers de projet, tests compris, sont directement à côté du `.slnx` (il n'y a ni dossier `src/`, ni dossier `tests/`).

Ce fichier donne la carte d'ensemble, les conventions transverses, les commandes et la feuille de route. Les règles de chaque couche (métier, persistance, sécurité, ViewModels, services natifs, langage visuel) sont dans le `CLAUDE.md` du projet concerné ; le langage visuel est dans `Finance.Android17.Maui/CLAUDE.md`.

**Modèle mono-solde, par choix.** La version 2 ajoute revenus, revenus récurrents, solde initial, prévision de fin de mois et objectifs d'épargne **sans comptes ni transferts**. Ne pas introduire de gestion multi-comptes ni de connexion bancaire sans nouvelle demande explicite ; un rapprochement manuel ne serait à étudier qu'après stabilisation de ce modèle.

## Projets

| Dossier | Cible | Rôle |
| --- | --- | --- |
| `Finance.Android17.Domain` | `net10.0` | Entités persistées, sans aucune dépendance. |
| `Finance.Android17.Application` | `net10.0` | Règles métier et calculs purs (tableau de bord, résumé financier, filtres, échéances, rappels, `ReceiptParser`…), services d'orchestration et **ports** (interfaces de persistance, de sécurité et de plateforme). |
| `Finance.Android17.Infrastructure` | `net10.0` | Implémentation des ports indépendants de la plateforme : persistance (dépôts EF Core + SQLite/SQLCipher, schéma versionné), PIN, clé de la base, sauvegarde chiffrée, export CSV. |
| `Finance.Android17.Presentation` | `net10.0` | ViewModels (CommunityToolkit.Mvvm) et modèles d'affichage, sans dépendance à MAUI. |
| `Finance.Android17.Maui` | `net10.0-android`, `net10.0-windows10.0.19041.0` | Application : pages XAML, ressources et fins adaptateurs vers la plateforme (stockage sécurisé, partage de fichiers, préférences, biométrie, OCR, rappels). Racine de composition. |
| `Finance.Android17.Tests` | `net10.0` | Tests xUnit : règles pures, dépôts sur une vraie base SQLCipher temporaire, sécurité et format de sauvegarde, ViewModels, composition. |

Chaque projet a son propre `CLAUDE.md` décrivant son contenu et ses règles.

## Architecture

Architecture en couches, dépendances dirigées vers le domaine :

```text
Maui ──► Presentation ──► Application ──► Domain
  │                          ▲
  └────► Infrastructure ─────┘  (Infrastructure référence aussi Domain)
```

`Presentation` référence `Application` et `Domain` ; `Finance.Android17.Tests` référence tout sauf `Maui`.

- `Domain` ne dépend de rien.
- `Application` référence `Domain`. Elle contient les règles et définit les **ports** : un par agrégat pour la persistance (`IExpenseRepository`, `ICategoryRepository`, `IIncomeRepository`, `IRecurringIncomeRepository`, `ISubscriptionRepository`, `ISavingsGoalRepository`, `IFinancialProfileRepository`, plus `IDueOperationsStore`, `IFinanceDataStore`, `IDatabaseInitializer`), puis la sécurité (`ISecretStore`, `IAppLockService`, `IBiometricService`), `ISettingsService`, `IExportService`, `IDataBackupService`, `IOcrService`, `IReminderService`, `IAlertPublisher`, `IDailyCheckScheduler`, et deux ports de plateforme très étroits : `ISecureValueStore` et `IFileExchange`.
- `Infrastructure` dépend de `Application` + `Domain` et implémente tout ce qui ne dépend pas de la plateforme : dépôts, PIN, clé de la base, format `.mfbackup`, CSV. Rien d'`internal` n'en sort : seul `AddInfrastructure()` est public.
- `Presentation` dépend d'`Application` + `Domain` : les ViewModels reçoivent des ports et services, jamais une API de plateforme.
- `Maui` référence `Application`, `Infrastructure` et `Presentation`. Il n'implémente que ce qui touche vraiment la plateforme (`ISecureValueStore` → `SecureStorage`, `IFileExchange` → partage/sélecteur de fichiers, `ISettingsService` → `Preferences`, `IBiometricService`, `IOcrService`, `IReminderService`, `IAlertPublisher`, `IDailyCheckScheduler`) et compose le tout dans `MauiProgram`.
- Les règles métier vivent dans `Application`, jamais dans le code-behind MAUI ni dans un ViewModel.

**Composition** : chaque couche expose sa propre méthode d'enregistrement, appelée par `MauiProgram` : `AddInfrastructure()`, `AddApplication()`, `AddPresentation()`. Les tests construisent la même composition avec des adaptateurs de plateforme factices.

**Horloge et base injectables** : toute heure vient d'un `TimeProvider` (jamais `DateTime.Now`/`Today` dans `Application`, `Infrastructure` ou `Presentation`), et le chemin de la base d'un `FinanceDatabaseOptions` : c'est ce qui rend l'ensemble testable.

Chemin type d'une opération : page MAUI → ViewModel (`Presentation`) → port d'un agrégat (`Application`) → dépôt (`Infrastructure/Persistence`) → `FinanceDatabase` → SQLCipher. Pour ajouter une opération de stockage : l'ajouter au port de l'agrégat, l'implémenter dans son dépôt, puis l'utiliser depuis un ViewModel ou un service. Pas de façade intermédiaire.

## Conventions transverses

- **Montants** : toujours des `long` en centimes (`AmountMinor`), jamais de `decimal` stocké. Arrondi des saisies avec `MidpointRounding.AwayFromZero`. Devise par défaut `EUR`.
- **Dates** : dates sans heure (`.Date`) pour les dépenses, revenus et échéances.
- **Suppressions logiques** (`DeletedAtUtc`) pour dépenses, catégories, abonnements, revenus, revenus récurrents et objectifs ; les lectures normales et les sauvegardes les ignorent.
- **Asynchrone** : les opérations persistantes acceptent un `CancellationToken` ; la base s'ouvre à la première utilisation (`IDatabaseInitializer.InitializeAsync` la prépare dès le déverrouillage).
- **Hors ligne** : pas de permission Internet, sauvegarde Android désactivée (`allowBackup=false`).
- **Sécurité** : ne pas changer la clé SQLCipher, le nom de la base (`finance-v2.db3`), les clés du coffre sécurisé ni le format de sauvegarde sans stratégie de migration explicite. Toute évolution du schéma passe par une **migration** de `FinanceSchema` (`PRAGMA user_version`).
- **Valeurs persistées** : les libellés français de `ThemeOptions` et `AfterSaveOptions` sont enregistrés dans les préférences existantes ; ne pas les modifier.
- **Langue** : interface, messages et commentaires du projet sont en français.
- `Directory.Packages.props` centralise les versions NuGet (`ManagePackageVersionsCentrally`) : ajouter les versions là, pas dans les `.csproj`.
- Un seul espace de noms par projet (`Finance.Android17.<Projet>`) : les dossiers servent à ranger, pas à nommer.
- `bin/` et `obj/` sont générés et ignorés par Git.

## Commandes

À lancer depuis ce dossier (celui du `.slnx`).

```bash
# Restauration NuGet
dotnet restore Finance.Android17.Maui/Finance.Android17.Maui.csproj

# Couches sans MAUI (compilent sur n'importe quel OS avec le SDK .NET 10)
dotnet build Finance.Android17.Domain/Finance.Android17.Domain.csproj -c Debug
dotnet build Finance.Android17.Application/Finance.Android17.Application.csproj -c Debug
dotnet build Finance.Android17.Infrastructure/Finance.Android17.Infrastructure.csproj -c Debug

# Application Android (workload maui-android requis)
dotnet build Finance.Android17.Maui/Finance.Android17.Maui.csproj -f net10.0-android -c Debug

# Tests (sans MAUI, environ deux minutes : une base SQLCipher temporaire par test de persistance)
dotnet test Finance.Android17.Tests/Finance.Android17.Tests.csproj -c Debug

# APK Release ARM64 non débogable (à utiliser sur appareil à pages de 16 ko)
dotnet publish Finance.Android17.Maui/Finance.Android17.Maui.csproj -f net10.0-android -c Release -r android-arm64
```

Sorties APK : `Finance.Android17.Maui/bin/Debug/net10.0-android/fr.nicol.finance.android17-Signed.apk` et `Finance.Android17.Maui/bin/Release/net10.0-android/android-arm64/publish/fr.nicol.finance.android17-Signed.apk`.

La cible Windows du projet MAUI ne se restaure que sous Windows (sinon `NETSDK1100`) ; sur Linux/macOS, compiler uniquement `-f net10.0-android`, et restaurer avec `-p:EnableWindowsTargeting=true`. Si le build échoue avec une erreur d'assets (`net10.0-android/android-x64` absent), relancer `dotnet restore` au lieu de modifier les fichiers générés.

Sur une machine sans Android SDK (environnement cloud, CI), la compilation Android s'arrête sur `XA5300` : installer le SDK Android (plateforme 36, build-tools 36) et passer `-p:AndroidSdkDirectory=<chemin>`. En Debug, l'APK est signé avec le keystore de débogage par défaut (`~/.android/debug.keystore`) : le `.csproj` ne définit pas `AndroidKeyStore`. En Release, il déclare `AndroidKeyStore=True` sans fournir de keystore : l'étape finale de signature échoue alors sur `XA4314` (`AndroidSigningKeyPass` vide), le code C# est compilé mais l'APK n'est pas produit ; fournir `AndroidSigningKeyStore`, `AndroidSigningStorePass`, `AndroidSigningKeyAlias` et `AndroidSigningKeyPass` (en `-p:` ou dans un `.csproj.user` non versionné). Les couches non-MAUI (`Domain`, `Application`, `Infrastructure`, `Presentation`) et les tests se compilent et s'exécutent sans cela. Sur ce genre de machine, `-p:AndroidSdkDirectory=<chemin> -p:EnableWindowsTargeting=true` suffit pour vérifier que le C# et le XAML (bindings compilés) compilent en Debug ; en Release, `XA4314` est alors la seule erreur attendue.

Avertissements connus à la compilation Android (non bloquants) : `CS0618`/`CS0672` dans `Platforms/Android/CameraCapture.cs` (API `Fragment.StartActivityForResult`/`OnActivityResult` obsolètes ; ce flux caméra est actuellement masqué dans l'interface) et `XA4301` (bibliothèques natives ML Kit `libmlkit_google_ocr_pipeline.so` dupliquées lors de l'empaquetage).

## Feuille de route envisagée

Pistes validées pour une version ultérieure, documentées pour mémoire : **ne pas les implémenter sans demande explicite.** Par ordre de priorité recommandé :

1. Accélérer la saisie : duplication d'une dépense, modèles favoris, suggestions locales de catégorie selon la description ou le commerçant (les catégories sont déjà triées par usage récent dans `AddExpenseViewModel`).
2. Exposer une corbeille pour consulter et restaurer les entités supprimées logiquement avant une éventuelle suppression définitive (aujourd'hui seule l'annulation immédiate d'une suppression de dépense existe).
3. Règles automatiques locales de catégorisation, puis import CSV avec aperçu, correspondance des colonnes et détection des doublons.
4. Widget Android pour l'ajout rapide et l'affichage du budget restant.

Déjà réalisé, à ne pas replanifier : budgets mensuels par catégorie (limite, progression et seuils 80 % / 100 % sur l'accueil, en plus du budget mensuel global), prévision de fin de mois avec alerte de découvert probable, objectifs d'épargne.
