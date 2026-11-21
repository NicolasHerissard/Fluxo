<#
.SYNOPSIS
  Compile l'APK Release de Fluxo sur ce PC, le signe avec la clé de production et le publie dans une release GitHub.
.DESCRIPTION
  Remplace le workflow GitHub « APK Android » quand les minutes gratuites de GitHub Actions sont épuisées.
  L'application (Réglages > Vérifier les mises à jour) lit la release publiée ici, exactement comme celle de la CI.
  Prérequis : .NET 10 + workload maui-android, Android SDK, GitHub CLI connecté (gh auth login), et la clé créée par
  configurer-signature.ps1 (dossier Fluxo-signature du profil). Le script met main à jour puis compile : il ne
  publie jamais autre chose que le dernier main.
.PARAMETER DossierSignature
  Dossier contenant fluxo-release.keystore et mot-de-passe.txt.
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\tools\publier-apk.ps1
#>
param(
    [string]$DossierSignature = (Join-Path $env:USERPROFILE 'Fluxo-signature')
)
$ErrorActionPreference = 'Stop'
$repo = 'NicolasHerissard/Fluxo'
$racine = Split-Path -Parent $PSScriptRoot
Set-Location $racine

function Exiger($commande, $aide) {
    if (-not (Get-Command $commande -ErrorAction SilentlyContinue)) { throw "$commande introuvable. $aide" }
}
Exiger git 'Installe Git.'
Exiger dotnet 'Installe le SDK .NET 10.'
Exiger gh 'Installe GitHub CLI (winget install GitHub.cli) puis lance gh auth login.'

$keystore = Join-Path $DossierSignature 'fluxo-release.keystore'
$fichierMdp = Join-Path $DossierSignature 'mot-de-passe.txt'
if (-not (Test-Path $keystore) -or -not (Test-Path $fichierMdp)) {
    throw "Clé introuvable dans $DossierSignature : lance d'abord tools\configurer-signature.ps1."
}
$mdp = (Get-Content $fichierMdp -Raw).Trim()

# Toujours publier le dernier main, jamais un travail local en cours.
if (git status --porcelain) { throw 'Des modifications locales non enregistrées existent : commit ou annule-les avant de publier.' }
git fetch origin main
git checkout main
git pull --ff-only origin main
$sha = (git rev-parse HEAD).Trim()
$code = [int](git rev-list --count HEAD).Trim()
Write-Host "Publication du commit $sha, numéro de build $code" -ForegroundColor Cyan

$tag = "apk-production-$code"
$ErrorActionPreference = 'Continue'   # PowerShell 5.1 : un stderr redirigé d'une commande native lèverait sinon une exception
gh release view $tag --repo $repo *> $null
$existe = ($LASTEXITCODE -eq 0)
$ErrorActionPreference = 'Stop'
if ($existe) { throw "La release $tag existe déjà : rien à publier (aucun nouveau commit depuis la dernière version)." }

Set-Location (Join-Path $racine 'Finance.Android17')
dotnet build 'Finance.Android17.Maui\Finance.Android17.Maui.csproj' -f net10.0-android -c Release `
    "-p:ApplicationVersion=$code" `
    "-p:AndroidSigningKeyStore=$keystore" "-p:AndroidSigningStorePass=$mdp" `
    '-p:AndroidSigningKeyAlias=fluxo' "-p:AndroidSigningKeyPass=$mdp"
if ($LASTEXITCODE -ne 0) { throw 'La compilation a échoué.' }

$apk = Get-ChildItem 'Finance.Android17.Maui\bin\Release\net10.0-android\android-arm64' -Filter '*-Signed.apk' | Select-Object -First 1
if (-not $apk) { throw 'APK signé introuvable après la compilation.' }
$asset = Join-Path ([IO.Path]::GetTempPath()) "fluxo-$code.apk"
Copy-Item $apk.FullName $asset -Force

# Signature finale avec apksigner, comme la CI (facultative si les outils Android ne sont pas trouvés).
$sdk = @($env:ANDROID_HOME, $env:ANDROID_SDK_ROOT, "$env:LOCALAPPDATA\Android\Sdk", "${env:ProgramFiles(x86)}\Android\android-sdk") |
       Where-Object { $_ -and (Test-Path "$_\build-tools") } | Select-Object -First 1
if ($sdk) {
    $apksigner = Get-ChildItem "$sdk\build-tools" -Filter apksigner.bat -Recurse | Sort-Object FullName -Descending | Select-Object -First 1
    $signer = $apksigner.FullName
    & $signer sign --ks $keystore --ks-pass "pass:$mdp" --key-pass "pass:$mdp" --ks-key-alias fluxo $asset
    if ($LASTEXITCODE -ne 0) { throw 'apksigner a échoué.' }
    & $signer verify $asset
    if ($LASTEXITCODE -ne 0) { throw "La signature de l'APK est invalide." }
} else {
    Write-Host 'apksigner introuvable : utilisation de la signature faite par dotnet.' -ForegroundColor Yellow
}

gh release create $tag $asset --repo $repo --title "APK production #$code" `
    --notes "APK Release arm64 signé en production, compilé sur le PC de Nicolas. Commit $sha." `
    --prerelease --target $sha
if ($LASTEXITCODE -ne 0) { throw 'La publication de la release a échoué.' }
Remove-Item $asset -ErrorAction SilentlyContinue
Write-Host "Terminé : https://github.com/$repo/releases/tag/$tag" -ForegroundColor Green
