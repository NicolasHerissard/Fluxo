# Finance.Android17 — vue d'ensemble de la solution

Application Android (et Windows) de gestion de finances personnelles, **locale, hors ligne et mono-solde** : dépenses, revenus ponctuels et récurrents, abonnements, objectifs d'épargne, solde initial. Nom affiché : « Mes Finances », identifiant Android `fr.nicol.finance.android17`, version `2.0.0` (code de version `8`). Stack : .NET 10, .NET MAUI, EF Core 10, SQLite chiffré avec SQLCipher, CommunityToolkit.Mvvm.

Le point d'entrée de la solution est `Finance.Android17.slnx` (format `.slnx`, Visual Studio 2026 ou SDK .NET 10). Les dossiers de projet sont directement à côté du `.slnx` (il n'y a plus de dossier `src/`).

Pour le détail des règles métier, de la sécurité, du langage visuel et de la feuille de route, lire `AGENTS.md` (même dossier) ; ce fichier-ci donne la carte d'ensemble et les commandes.

## Projets

| Dossier | Cible | Rôle |
| --- | --- | --- |
| `Finance.Android17.Domain` | `net10.0` | Entités persistées, sans aucune dépendance. |
| `Finance.Android17.Application` | `net10.0` | Contrats (interfaces, DTO de sauvegarde), `FinanceService`, règles de calcul pures, `ReceiptParser`. |
| `Finance.Android17.Infrastructure` | `net10.0` | Persistance : `EncryptedFinanceStore` (EF Core + SQLite/SQLCipher). |
| `Finance.Android17.Presentation` | `net10.0` | ViewModels (CommunityToolkit.Mvvm) et modèles d'affichage. **Absent du dépôt pour l'instant**, voir plus bas. |
| `Finance.Android17.Maui` | `net10.0-android`, `net10.0-windows10.0.19041.0` | Application : pages XAML, DI, services natifs, ressources. Racine de composition. |
| `tests/Finance.Android17.Tests` | `net10.0` | Tests xUnit des règles pures (dashboard, échéances). **Absent du dépôt pour l'instant.** |

Chaque projet présent a son propre `CLAUDE.md` décrivant son contenu et ses règles.

## Architecture

Architecture en couches, dépendances dirigées vers le domaine :

```text
Maui ──► Presentation ──► Application ──► Domain
  │                          ▲
  └────► Infrastructure ─────┘  (Infrastructure référence aussi Domain)
```

Les références de `Maui` et d'`Infrastructure` ont été vérifiées dans les `.csproj` ; celle de `Presentation` vers `Application` est déduite d'`AGENTS.md`, le projet n'étant pas dans le dépôt.

- `Domain` ne dépend de rien.
- `Application` référence `Domain` et définit les abstractions (`IFinanceStore`, `ISecretStore`, `IAppLockService`, `IBiometricService`, `ISettingsService`, `IExportService`, `IOcrService`, `IDataBackupService`, `IReminderService`).
- `Infrastructure` implémente `IFinanceStore` (seule implémentation : `EncryptedFinanceStore`) et dépend de `Application` + `Domain`.
- `Maui` référence `Application`, `Infrastructure` et `Presentation` ; il implémente les abstractions natives (PIN, biométrie, réglages, CSV, OCR, sauvegarde, rappels) et enregistre tout dans `MauiProgram`.
- Les règles métier et abstractions réutilisables vivent dans `Application`, jamais dans le code-behind MAUI.

Chemin type d'une opération : page MAUI → ViewModel (`Presentation`) → `FinanceService` (`Application`) → `IFinanceStore` → `EncryptedFinanceStore` (`Infrastructure`). Pour ajouter une opération de stockage : l'ajouter à `IFinanceStore`, l'implémenter dans `EncryptedFinanceStore`, l'exposer par `FinanceService`, puis l'utiliser dans un ViewModel.

## Conventions transverses

- **Montants** : toujours des `long` en centimes (`AmountMinor`), jamais de `decimal` stocké. Arrondi des saisies avec `MidpointRounding.AwayFromZero`. Devise par défaut `EUR`.
- **Dates** : dates sans heure (`.Date`) pour les dépenses, revenus et échéances.
- **Suppressions logiques** (`DeletedAtUtc`) pour dépenses, catégories, abonnements, revenus, revenus récurrents et objectifs ; les lectures normales et les sauvegardes les ignorent.
- **Asynchrone** : les opérations persistantes passent par `InitializeAsync` et acceptent un `CancellationToken`.
- **Hors ligne** : pas de permission Internet, sauvegarde Android désactivée (`allowBackup=false`).
- **Sécurité** : ne pas changer la clé SQLCipher, le nom de la base (`finance-v2.db3`) ni le format de sauvegarde sans stratégie de migration explicite.
- **Langue** : interface, messages et commentaires du projet sont en français.
- `Directory.Packages.props` centralise les versions NuGet (`ManagePackageVersionsCentrally`) : ajouter les versions là, pas dans les `.csproj`.
- `bin/` et `obj/` sont générés et ignorés par Git. Les fichiers `Class1.cs` sont des placeholders de template sans logique.

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

# Tests (quand le projet de tests est présent)
dotnet test tests/Finance.Android17.Tests/Finance.Android17.Tests.csproj -c Debug

# APK Release ARM64 non débogable (à utiliser sur appareil à pages de 16 ko)
dotnet publish Finance.Android17.Maui/Finance.Android17.Maui.csproj -f net10.0-android -c Release -r android-arm64
```

Sorties APK : `Finance.Android17.Maui/bin/Debug/net10.0-android/fr.nicol.finance.android17-Signed.apk` et `Finance.Android17.Maui/bin/Release/net10.0-android/android-arm64/publish/fr.nicol.finance.android17-Signed.apk`.

La cible Windows du projet MAUI ne se restaure que sous Windows (sinon `NETSDK1100`) ; sur Linux/macOS, compiler uniquement `-f net10.0-android`. Si le build échoue avec une erreur d'assets (`net10.0-android/android-x64` absent), relancer `dotnet restore` au lieu de modifier les fichiers générés.

## État actuel du dépôt

`Finance.Android17.slnx` et `Finance.Android17.Maui.csproj` référencent le projet `Finance.Android17.Presentation` et le projet de tests `tests/Finance.Android17.Tests`, mais **ces deux dossiers ne sont pas (encore) versionnés** : le projet MAUI ne peut donc pas être restauré ni compilé tant que `Presentation` est absent. Seuls `Domain`, `Application` et `Infrastructure` compilent seuls. Une fois ces dossiers ajoutés, créer leur `CLAUDE.md` et mettre à jour ce paragraphe.
