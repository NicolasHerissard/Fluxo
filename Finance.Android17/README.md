# Finance.Android17

Application locale et hors ligne de gestion des dépenses, revenus, solde et objectifs d’épargne.

## Compatibilité

- .NET 10 / .NET MAUI 10
- cible Android : API 36 (`net10.0-android`)
- testée sur Google Pixel 10 sous Android 17 / API 37
- cible Windows : `net10.0-windows10.0.19041.0`
- identifiant Android : `fr.nicol.finance.android17`

API 36 est la cible Android prise en charge par le SDK .NET 10 installé. Android assure la compatibilité ascendante de l'application sur Android 17.

## Sécurité et stabilité

- PIN de 4 à 8 chiffres conservé sous forme dérivée PBKDF2 avec sel aléatoire.
- calcul PBKDF2 exécuté hors du thread d'interface pour éviter les blocages et ANR.
- clé SQLCipher aléatoire de 256 bits stockée dans le magasin sécurisé de la plateforme.
- base SQLite locale chiffrée.
- APK Debug autonome : les assemblies sont intégrées et ne dépendent pas du Fast Deployment.
- aucune ressource `InvertedBoolConverter` ni convertisseur XAML requis.
- APK Release ARM64 non débogable, validé pour l'alignement 16 ko.
- aucune permission Internet dans le MVP hors connexion.
- sauvegarde Android désactivée pour ne pas extraire la base chiffrée de son contexte de clé.

## Interface 2.0.0

- navigation inférieure : Accueil, Dépenses, Finances, Abonnements et Réglages ;
- dashboard semaine/mois/année avec solde actuel, revenus, résultat net et prévision de fin de mois ;
- ajout et modification guidés des dépenses ;
- historique avec recherche instantanée, filtres, suppression et annulation ;
- revenus ponctuels et revenus récurrents mensuels, trimestriels ou annuels ;
- solde initial configurable et calcul automatique du solde global mono-compte ;
- objectifs d’épargne avec montant atteint, cible et progression ;
- abonnements mensuels, trimestriels ou annuels avec prochaines échéances ;
- catégories personnalisables ;
- masquage des montants, thème clair/sombre/système et verrouillage automatique ;
- déverrouillage biométrique facultatif ;
- export CSV via la feuille de partage native.
- sauvegarde chiffrée de format 2, avec restauration des sauvegardes de format 1.

## Commandes utiles

```powershell
dotnet build .\Finance.Android17.Maui\Finance.Android17.Maui.csproj -f net10.0-android -c Debug
dotnet test .\tests\Finance.Android17.Tests\Finance.Android17.Tests.csproj -c Debug
```

APK Debug signé généré :

```text
Finance.Android17.Maui\bin\Debug\net10.0-android\fr.nicol.finance.android17-Signed.apk
```

Pour installer sur un appareil Android 16 ko, utiliser la version **Release ARM64 non débogable** afin d'éviter les fichiers d'assemblies individuels du mode Debug :

```powershell
dotnet publish .\Finance.Android17.Maui\Finance.Android17.Maui.csproj -f net10.0-android -c Release -r android-arm64
```

APK Release :

```text
Finance.Android17.Maui\bin\Release\net10.0-android\android-arm64\publish\fr.nicol.finance.android17-Signed.apk
```

Ouvrir `Finance.Android17.slnx` dans Visual Studio 2026 ou avec un SDK .NET prenant en charge le format `.slnx`.
