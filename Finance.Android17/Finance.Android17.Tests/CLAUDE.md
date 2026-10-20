# Finance.Android17.Tests

Projet de **tests unitaires xUnit** (`net10.0`, `IsPackable=false`) de la solution. Il référence `Finance.Android17.Application` et `Finance.Android17.Domain`, et les packages `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` et `coverlet.collector` (versions dans `../Directory.Packages.props`). `Xunit` est importé globalement par le `.csproj`.

Il ne référence ni `Infrastructure`, ni `Presentation`, ni `Maui` : il vise uniquement les règles pures d'`Application`.

## Contenu

`UnitTest1.cs` : la classe `DashboardTests` (7 tests) :

- `ReceiptParser` reconnaît total TTC, date et commerçant sur un ticket français ;
- `FinanceService.BuildDashboard` ne compte que le mois courant et trouve la catégorie principale ;
- `FinanceService.GetDueDates` renvoie toutes les échéances manquées jusqu'à aujourd'hui, et retombe à un mois pour un intervalle invalide ;
- `FinanceService.BuildFinancialSummary` calcule solde, résultat net, prévision de fin de mois et taux d'épargne, sans division par zéro quand il n'y a aucun revenu ;
- un snapshot de sauvegarde de format 1 se désérialise avec des données de format 2 vides (compatibilité ascendante de `FinanceBackupSnapshot`).

## Commandes

Depuis le dossier de la solution :

```bash
dotnet test Finance.Android17.Tests/Finance.Android17.Tests.csproj -c Debug
```

Les tests ne nécessitent ni MAUI, ni Android : ils s'exécutent sur n'importe quel OS avec le SDK .NET 10.

## Règles

- Tester ici les règles pures d'`Application` : calculs de dashboard et de résumé financier, échéances, `ReceiptParser`, compatibilité des formats de sauvegarde. Toute nouvelle règle de ce type doit venir avec un test.
- Les helpers de `Presentation` (`Money`, `TryAmount`…) sont `internal` et ne sont pas visibles d'ici ; pour les tester, ajouter explicitement `InternalsVisibleTo` et la référence de projet plutôt que de rendre ces membres publics.
- Les dates des tests sont fixes (septembre/octobre 2026) : ne pas dépendre de `DateTime.Today` pour que les tests restent déterministes.
- Montants en centimes (`long`), comme dans le reste de la solution.
- Il n'existe pas encore de test d'intégration pour `EncryptedFinanceStore` (SQLCipher, schéma, sauvegarde/restauration) ni de test des ViewModels.
