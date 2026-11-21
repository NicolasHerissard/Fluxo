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

# keytool n'est pas toujours dans le PATH : on le cherche dans les emplacements habituels de Java (JDK, Android Studio, Visual Studio).
$keytool = (Get-Command keytool.exe -ErrorAction SilentlyContinue).Source
if (-not $keytool) {
    $racines = @($env:JAVA_HOME, "$env:ProgramFiles\Java", "$env:ProgramFiles\Microsoft", "$env:ProgramFiles\Eclipse Adoptium",
                 "$env:ProgramFiles\Android", "${env:ProgramFiles(x86)}\Android", "$env:ProgramFiles\Android\Android Studio\jbr") |
              Where-Object { $_ -and (Test-Path $_) }
    $keytool = $racines | ForEach-Object { Get-ChildItem $_ -Filter keytool.exe -Recurse -ErrorAction SilentlyContinue } |
               Select-Object -First 1 -ExpandProperty FullName
}
if (-not $keytool) { throw "keytool introuvable : installe un JDK (par ex. winget install Microsoft.OpenJDK.21), ouvre un nouveau PowerShell et relance." }
Write-Host "keytool : $keytool"

& $keytool -genkeypair -keystore $ks -storepass $pass -keypass $pass -alias fluxo `
  -keyalg RSA -keysize 2048 -validity 10000 -dname 'CN=Fluxo' | Out-Null
if (-not (Test-Path $ks)) { throw 'keytool a échoué (Java est-il installé et dans le PATH ?).' }

$b64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($ks))
gh secret set ANDROID_KEYSTORE_BASE64   --repo $repo --body $b64
gh secret set ANDROID_KEYSTORE_PASSWORD --repo $repo --body $pass
gh secret set ANDROID_KEY_ALIAS         --repo $repo --body 'fluxo'

Set-Content -Path (Join-Path $dir 'mot-de-passe.txt') -Value $pass
Write-Host "Terminé. Sauvegarde le dossier $dir (clé + mot de passe) dans un endroit sûr : sans elle, plus de mise à jour possible." -ForegroundColor Green
