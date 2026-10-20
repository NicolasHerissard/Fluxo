# Finance.Android17 — guide pour les agents

## Vue d’ensemble

Finance.Android17 est une application de gestion financière personnelle mono-solde, locale et hors ligne, construite avec .NET 10 et .NET MAUI. Elle gère les dépenses, revenus, récurrences et objectifs d’épargne. Le point d’entrée de la solution est `Finance.Android17.slnx`.

La cible réellement déclarée par le projet MAUI est `net10.0-android` et `net10.0-windows10.0.19041.0`. L’application Android utilise l’identifiant `fr.nicol.finance.android17`. Le README décrit Android API 36 comme cible et Android 17/API 37 comme environnement de test.

## Architecture

Le code est organisé en couches, avec des dépendances dirigées vers le domaine :

- `Finance.Android17.Domain` : entités persistées (`Entity`, `Expense`, `Income`, `RecurringIncome`, `SavingsGoal`, `FinancialProfile`, `Category`, `Subscription`). Les montants sont des `long` en centimes (`AmountMinor`) ; ne pas utiliser `decimal` pour les stocker.
- `Finance.Android17.Application` : contrats (`IFinanceStore`, services de sécurité, réglages, export et sauvegarde), DTO de sauvegarde, `FinanceService` et règles calculées indépendantes de l’UI.
- `Finance.Android17.Infrastructure` : `FinanceDbContext` EF Core et `EncryptedFinanceStore`, persistance SQLite chiffrée avec SQLCipher.
- `Finance.Android17.Presentation` : ViewModels CommunityToolkit.Mvvm et modèles d’affichage (`ExpenseRow`, `ChartBar`, etc.).
- `Finance.Android17.Maui` : application MAUI, pages XAML/code-behind, enregistrement DI, services natifs et ressources.
- `Finance.Android17.Tests` : tests xUnit des règles pures de dashboard et d’échéances d’abonnements.

Les projets non-MAUI ciblent `net10.0`. Les versions NuGet sont centralisées dans `Directory.Packages.props`.

## Démarrage et navigation

`MauiProgram.CreateMauiApp` enregistre les services métier en singleton et les pages/ViewModels en transient. La règle importante est de ne pas réutiliser les pages MAUI après un remplacement de `Window.Page`, car le contexte MAUI Android peut être détruit.

`App` démarre sur une `LockPage`. Après PIN ou biométrie, la fenêtre est remplacée par `AppShell`, qui construit quatre onglets : Accueil, Dépenses, Abonnements et Réglages. Après une période d’inactivité, `App` remplace de nouveau la Shell par l’écran de verrouillage.

Flux de déverrouillage :

1. `LockViewModel` vérifie si un PIN existe.
2. Au premier démarrage, le PIN est enregistré ; sinon il est vérifié.
3. `PrepareFinanceAsync` initialise la base, traite éventuellement les abonnements échus et planifie les rappels.
4. L’événement `Unlocked` demande à `App` d’afficher la Shell.

## Domaine et persistance

`EncryptedFinanceStore` :

- initialise SQLCipher une seule fois, sous `SemaphoreSlim` ;
- récupère ou crée une clé de 256 bits dans `SecureStorage` ;
- place `finance-v2.db3` dans `Environment.SpecialFolder.LocalApplicationData` ;
- crée la base et applique les ajustements de schéma nécessaires au démarrage ;
- injecte cinq catégories par défaut si aucune catégorie n’existe ;
- masque les entités dont `DeletedAtUtc` n’est pas nul dans les lectures normales ;
- met à jour `UpdatedAtUtc` et `Version` lors des modifications ;
- charge la catégorie d’une dépense avec `Include`.

Les suppressions de dépenses, catégories et abonnements sont logiques. Une catégorie ne peut pas être supprimée tant qu’une dépense active la référence. `DeleteAllDataAsync` efface les données puis recrée les cinq catégories par défaut.

Les opérations d’abonnement échues sont transactionnelles. `ProcessDueSubscriptionsAsync` crée la catégorie `Abonnements` si nécessaire, crée une dépense pour chaque occurrence manquée jusqu’à la date du jour, puis avance `NextBillingDate`.

Les sauvegardes utilisent `FinanceBackupSnapshot` de format 2 et incluent catégories, dépenses, revenus, revenus récurrents, objectifs, abonnements et solde initial. La restauration accepte encore le format 1, initialise alors les nouvelles données à vide, remplace intégralement les données dans une transaction et refuse les versions inconnues.

## Sécurité

`AppSecurityService` stocke dans le magasin sécurisé de la plateforme :

- un sel et le hash PBKDF2-SHA256 du PIN, avec 100 000 itérations ;
- une clé aléatoire de base de données de 32 octets.

Le calcul PBKDF2 est exécuté hors du thread UI. La vérification du hash utilise `FixedTimeEquals`. Le PIN n’est jamais écrit dans les préférences ordinaires.

`EncryptedBackupService` produit des fichiers `.mfbackup` : JSON du snapshot chiffré avec AES-GCM, sel de 16 octets, nonce de 12 octets, tag de 16 octets et clé PBKDF2-SHA256 de 32 octets avec 200 000 itérations. Le mot de passe de sauvegarde doit avoir au moins 8 caractères. Les fichiers sont écrits dans le cache puis partagés via la feuille native ; l’import passe par le sélecteur de fichiers.

## Fonctionnalités UI

- `DashboardViewModel` calcule les vues semaine/mois/année, la comparaison avec la période précédente, les trois dépenses récentes, les catégories principales et le budget mensuel.
- `ExpensesViewModel` charge toutes les dépenses puis applique côté application la recherche, les catégories, les dates, les montants minimum/maximum et le tri. La recherche est retardée par un `CancellationTokenSource`.
- `AddExpenseViewModel` valide les montants, convertit les valeurs en centimes, gère l’ajout/modification et peut garder le formulaire ouvert.
- `SubscriptionsViewModel` gère les fréquences mensuelle, trimestrielle et annuelle, les totaux mensuel/annuel et les rappels.
- `SettingsViewModel` gère thème, devise, confidentialité des montants, budget, catégorie par défaut, PIN, biométrie, export CSV, sauvegarde/restauration et remise à zéro.
- `IncomeViewModel` gère le solde initial, les revenus ponctuels, les revenus récurrents, les objectifs d’épargne et les indicateurs financiers mono-solde.

Les réglages simples sont persistés par `Preferences` via `AppSettingsService`. Les montants visibles peuvent être remplacés par `••••`, mais les données restent stockées normalement.

## Services natifs

- `BiometricService` est réellement actif sous Android ; il retourne indisponible sur les autres plateformes.
- `ReminderService` utilise `AlarmManager`, notifications Android et le canal `subscription-reminders`. Android 13+ demande `POST_NOTIFICATIONS`.
- `CsvExportService` génère un CSV séparé par `;`, encodé UTF-8 avec BOM, puis ouvre le partage natif.
- `OcrService` utilise le binding Android de ML Kit Text Recognition avec le modèle Latin embarqué ; le texte est ensuite analysé par `ReceiptParser` avant préremplissage de la saisie.
- `ColorContrastConverter` et les ressources XAML sont spécifiques à la présentation ; éviter d’ajouter un convertisseur XAML générique sans vérifier les bindings existants.

## Règles de modification

- Garder les règles métier et les abstractions réutilisables dans `Application`, jamais dans le code-behind MAUI.
- Ajouter une opération de stockage à `IFinanceStore`, puis l’exposer par `FinanceService` avant de l’utiliser dans un ViewModel.
- Respecter les centimes (`long`) et la conversion existante `TryAmount`; utiliser `MidpointRounding.AwayFromZero` pour les saisies monétaires.
- Utiliser les dates sans heure pour les dépenses et échéances (`.Date`) afin d’éviter les décalages de comparaison.
- Faire passer les opérations persistantes par `InitializeAsync` et accepter le `CancellationToken`.
- Préserver les suppressions logiques dans les lectures et les sauvegardes normales, sauf si une fonctionnalité demande explicitement la récupération technique des données supprimées.
- Ne pas modifier la clé SQLCipher, le nom de la base ou le format de backup sans stratégie de migration explicite.
- Toute nouvelle page/ViewModel doit être enregistrée dans `MauiProgram`; les pages affichées après changement de `Window.Page` doivent rester transient.
- Pour le scan de ticket, conserver la séparation `IOcrService`/`ReceiptParser` et toujours laisser l’utilisateur confirmer les champs OCR avant sauvegarde.
- Pour Android, éviter les permissions ou le réseau non nécessaires : l’application est conçue comme un MVP hors ligne.

Les fichiers `bin/` et `obj/` sont générés et ne doivent pas être documentés ni modifiés manuellement. Les classes `Class1.cs` sont des placeholders issus des templates et ne contiennent aucune logique.

## Commandes utiles

Depuis la racine du projet :

```powershell
dotnet restore .\Finance.Android17.Maui\Finance.Android17.Maui.csproj
dotnet build .\Finance.Android17.Maui\Finance.Android17.Maui.csproj -f net10.0-android -c Debug
dotnet test .\Finance.Android17.Tests\Finance.Android17.Tests.csproj -c Debug
dotnet publish .\Finance.Android17.Maui\Finance.Android17.Maui.csproj -f net10.0-android -c Release -r android-arm64
```

La publication Release Android vise ARM64, produit un APK et désactive le shared runtime. Le README indique les chemins de sortie attendus sous `Finance.Android17.Maui/bin/...`.

## Vérification connue au moment de la rédaction

Les tests exécutés localement passent : 4 tests réussis. La compilation Debug Android passe sans erreur. Les avertissements restants concernent l’ancien flux caméra Android, actuellement masqué dans l’interface, et les bibliothèques natives ML Kit dupliquées lors de l’empaquetage. Une première compilation `--no-restore` avait échoué parce que le fichier d’assets NuGet ne contenait pas `net10.0-android/android-x64`; si cette erreur réapparaît, commencer par `dotnet restore` plutôt que modifier les fichiers générés.

Le README et le projet MAUI décrivent la version `2.0.0` (`ApplicationVersion` `8`) : lors d’une mise à jour de version, vérifier et synchroniser ces deux sources.

## Feuille de route envisagée pour une future version

Ces éléments sont des pistes validées pour une évolution ultérieure. Ils sont documentés ici pour mémoire et ne doivent pas être implémentés sans demande explicite.

Priorité recommandée :

1. Ajouter des budgets mensuels par catégorie, tout en conservant le budget mensuel global actuel. Chaque catégorie pourrait recevoir une limite, une progression consommée/restante et des seuils visuels à 80 % et 100 %.
2. Ajouter une prévision de fin de mois fondée sur les dépenses déjà saisies et les abonnements restant à payer, avec une alerte en cas de dépassement probable.
3. Accélérer la saisie avec la duplication d’une dépense, des modèles favoris et des suggestions locales de catégorie selon la description ou le commerçant.
4. Exposer une corbeille permettant de consulter et restaurer les entités supprimées logiquement avant leur éventuelle suppression définitive.
5. Ajouter des règles automatiques locales de catégorisation, puis un import CSV avec aperçu, correspondance des colonnes et détection des doublons.
6. Envisager ensuite un widget Android pour l’ajout rapide et l’affichage du budget restant, ainsi que des objectifs d’épargne.

La version 2 adopte volontairement un modèle mono-solde : elle ajoute revenus, revenus récurrents, solde initial, prévision et objectifs d’épargne sans comptes ni transferts. Ne pas introduire de gestion multi-comptes ou de connexion bancaire sans nouvelle demande explicite. Un rapprochement manuel ne devrait être étudié qu’après stabilisation de ce modèle.

## Langage visuel

L’application suit un thème proche de Material 3. La palette est centralisée dans `Resources/Styles/Colors.xaml`, les styles MAUI implicites dans `Resources/Styles/Styles.xaml` et les composants nommés dans `App.xaml` :

- couleur d’accent principale `#6750A4`, conteneur principal clair `#EADDFF`, danger `#B3261E` et succès `#2E7D32` ; fonds de page `#F7F5FA` en clair et `#121212` en sombre ;
- texte principal `#201A2B` en clair et `#F2EDF7` en sombre, texte secondaire `#625B71` et `#CAC4D0` ;
- police Open Sans, titres de page à 28, titres de section généralement à 20, corps à 14 et légendes à 13 ;
- espacements récurrents de 8, 12, 16 et 20 ; les pages utilisent généralement 20 de marge latérale ;
- rayons récurrents de 12 à 18 pour les contrôles et cartes, 24 pour les grands panneaux et 28 pour les boutons flottants ;
- les cartes emploient `Border`, un fond blanc ou `#211F26` et un trait discret `#E7E0EC` ou `#49454F` ; ne pas réintroduire `Frame` ;
- les ombres sont réservées aux éléments flottants ; le grand résumé du dashboard est le seul élément utilisant un dégradé ;
- les états sélectionnés utilisent le violet principal, un texte contrasté et, pour les catégories colorées, un contour clair accompagné d’une coche ;
- employer `InputField` autour des `Entry`, `Picker` et `DatePicker`, ainsi que `InputEntry` sur les champs texte ; les mappings Android globaux retirent leur soulignement natif ;
- employer `PrimaryButton`, `SecondaryButton`, `TextButton`, `DangerButton`, `SegmentButton`, `Fab` ou `ExtendedFab` au lieu de recopier couleurs et dimensions dans les pages ;
- les barres système Android sont gérées globalement par `SystemBarAppearance`, reprennent le fond clair ou sombre et adaptent la couleur de leurs icônes, y compris lors d’un changement de thème ;
- le choix Clair/Sombre/Système est appliqué immédiatement par `AppSettingsService` ;
- privilégier les contrôles MAUI actuels déjà employés : `Border`, `CollectionView`, `Grid`, `DatePicker`, `ProgressBar`, `AppThemeBinding`, états visuels et bindings MVVM.
