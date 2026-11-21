<#
.SYNOPSIS
  Télécharge le dernier APK de production de Fluxo dans un dossier du PC (par défaut %USERPROFILE%\Fluxo-versions).
.DESCRIPTION
  Lit les releases GitHub publiées par la CI, télécharge l'APK « fluxo-<build>.apk » le plus récent s'il n'est pas déjà
  présent, vérifie son empreinte SHA-256, puis ouvre le dossier. Aucune donnée personnelle n'est envoyée.
.PARAMETER Dossier
  Dossier de destination.
.PARAMETER Garder
  Nombre de versions conservées dans le dossier (les plus anciennes sont supprimées). 0 = tout garder.
.PARAMETER ActiverAuDemarrage
  Crée un raccourci dans le dossier Démarrage de Windows : le script se lancera à chaque ouverture de session.
.PARAMETER DesactiverAuDemarrage
  Supprime ce raccourci.
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\telecharger-derniere-version.ps1
  powershell -ExecutionPolicy Bypass -File .\telecharger-derniere-version.ps1 -ActiverAuDemarrage
#>
param(
    [string]$Dossier = (Join-Path $env:USERPROFILE 'Fluxo-versions'),
    [int]$Garder = 5,
    [switch]$ActiverAuDemarrage,
    [switch]$DesactiverAuDemarrage,
    [switch]$Silencieux
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$repo = 'NicolasHerissard/Fluxo'
$raccourci = Join-Path ([Environment]::GetFolderPath('Startup')) 'Fluxo - dernière version.lnk'

if ($DesactiverAuDemarrage) {
    Remove-Item $raccourci -ErrorAction SilentlyContinue
    Write-Host 'Lancement au démarrage désactivé.'
    return
}
if ($ActiverAuDemarrage) {
    $shell = New-Object -ComObject WScript.Shell
    $lien = $shell.CreateShortcut($raccourci)
    $lien.TargetPath = (Get-Command powershell.exe).Source
    $lien.Arguments = "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$PSCommandPath`" -Dossier `"$Dossier`" -Garder $Garder -Silencieux"
    $lien.Save()
    Write-Host "Lancement au démarrage activé : $raccourci"
}

try {
    $releases = Invoke-RestMethod "https://api.github.com/repos/$repo/releases?per_page=30" -Headers @{ 'User-Agent' = 'Fluxo-script' }
} catch {
    if (-not $Silencieux) { Write-Host "Impossible de joindre GitHub : $($_.Exception.Message)" -ForegroundColor Red }
    exit 1
}

$candidats = foreach ($release in $releases) {
    if ($release.draft) { continue }
    foreach ($asset in $release.assets) {
        if ($asset.name -match '^fluxo-(\d+)\.apk$' -and $asset.browser_download_url -like 'https://github.com/*') {
            [pscustomobject]@{ Build = [int]$Matches[1]; Asset = $asset }
        }
    }
}
$derniere = $candidats | Sort-Object Build -Descending | Select-Object -First 1
if (-not $derniere) {
    if (-not $Silencieux) { Write-Host 'Aucun APK de production publié pour le moment.' -ForegroundColor Yellow }
    exit 0
}

New-Item -ItemType Directory -Force $Dossier | Out-Null
$cible = Join-Path $Dossier $derniere.Asset.name
if (Test-Path $cible) {
    if (-not $Silencieux) { Write-Host "Déjà à jour : $cible" -ForegroundColor Green }
    exit 0
}

$temp = "$cible.partiel"
Invoke-WebRequest $derniere.Asset.browser_download_url -OutFile $temp -Headers @{ 'User-Agent' = 'Fluxo-script' }
if ($derniere.Asset.digest -like 'sha256:*') {
    $attendu = $derniere.Asset.digest.Substring(7).ToLowerInvariant()
    $obtenu = (Get-FileHash $temp -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($obtenu -ne $attendu) { Remove-Item $temp; throw 'Fichier corrompu (empreinte différente) : téléchargement supprimé.' }
}
Move-Item $temp $cible

if ($Garder -gt 0) {
    Get-ChildItem $Dossier -Filter 'fluxo-*.apk' | Sort-Object { [int]($_.BaseName -replace '\D', '') } -Descending |
        Select-Object -Skip $Garder | Remove-Item
}
Write-Host "Nouvelle version téléchargée : $cible" -ForegroundColor Green
if (-not $Silencieux) { Start-Process explorer.exe $Dossier }
