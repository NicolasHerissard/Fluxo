# Génère la clé de signature de Fluxo sur CE PC et l'enregistre dans les secrets GitHub du dépôt.
# La clé et le mot de passe ne quittent jamais ton ordinateur, sauf vers les secrets GitHub (chiffrés).
# Prérequis : Java (keytool) et GitHub CLI connecté (gh auth login).
$ErrorActionPreference = 'Stop'
$repo = 'NicolasHerissard/Fluxo'
$dir  = Join-Path $env:USERPROFILE 'Fluxo-signature'
$ks   = Join-Path $dir 'fluxo-release.keystore'

if (Test-Path $ks) { throw "Une clé existe déjà : $ks. Ne l'écrase pas (les mises à jour ne marcheraient plus)." }
New-Item -ItemType Directory -Force $dir | Out-Null

$bytes = New-Object byte[] 32
[Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
$pass = ([Convert]::ToBase64String($bytes) -replace '[^A-Za-z0-9]', '').Substring(0, 24)

keytool -genkeypair -keystore $ks -storepass $pass -keypass $pass -alias fluxo `
  -keyalg RSA -keysize 2048 -validity 10000 -dname 'CN=Fluxo' | Out-Null
if (-not (Test-Path $ks)) { throw 'keytool a échoué (Java est-il installé et dans le PATH ?).' }

$b64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($ks))
gh secret set ANDROID_KEYSTORE_BASE64   --repo $repo --body $b64
gh secret set ANDROID_KEYSTORE_PASSWORD --repo $repo --body $pass
gh secret set ANDROID_KEY_ALIAS         --repo $repo --body 'fluxo'

Set-Content -Path (Join-Path $dir 'mot-de-passe.txt') -Value $pass
Write-Host "Terminé. Sauvegarde le dossier $dir (clé + mot de passe) dans un endroit sûr : sans elle, plus de mise à jour possible." -ForegroundColor Green
