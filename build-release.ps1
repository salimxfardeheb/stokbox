<#
.SYNOPSIS
    Produit l'installateur de Stokbox : dist\Stokbox-Setup-<version>.exe.

.DESCRIPTION
    1. vérifie les prérequis (SDK .NET, Inno Setup 6, logo, programme d'installation de .NET Framework 4.8) ;
    2. génère l'icône de l'application si elle manque ou si icon.png a changé ;
    3. compile la solution en Release ;
    4. lance tous les tests ;
    5. compile l'installateur avec Inno Setup.
    Le script s'arrête à la première erreur. La version vient de Directory.Build.props.

.PARAMETER WithoutDotNetRedist
    Produit un installateur qui n'embarque pas .NET Framework 4.8 (il demandera de l'installer à la main s'il manque).
    À réserver aux essais : l'installateur livré doit l'embarquer pour Windows 7.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build-release.ps1
#>
[CmdletBinding()]
param(
    [switch]$WithoutDotNetRedist
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$solution = Join-Path $root 'Stokbox.sln'
$appOutput = Join-Path $root 'src\Stokbox.App\bin\Release\net48'
$installerScript = Join-Path $root 'installer\stokbox.iss'
$redist = Join-Path $root 'installer\redist\ndp48-x86-x64-allos-enu.exe'
$iconSource = Join-Path $root 'assets\icon.png'
$icon = Join-Path $root 'assets\stokbox.ico'
$logo = Join-Path $root 'assets\logo.png'
$dist = Join-Path $root 'dist'

function Write-Step([string]$message) {
    Write-Host ''
    Write-Host "== $message" -ForegroundColor Cyan
}

function Find-Dotnet {
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $default = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
    if (Test-Path -LiteralPath $default) { return $default }

    return $null
}

function Find-InnoSetupCompiler {
    $command = Get-Command iscc -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $candidates = @()
    if (${env:ProgramFiles(x86)}) { $candidates += Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe' }
    if ($env:ProgramFiles) { $candidates += Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe' }

    # Installation dans un autre dossier : le registre connaît l'emplacement.
    foreach ($key in 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1',
                     'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1',
                     'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1') {
        $location = (Get-ItemProperty -Path $key -ErrorAction SilentlyContinue | ForEach-Object { $_.InstallLocation })
        if ($location) { $candidates += Join-Path $location 'ISCC.exe' }
    }

    return $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

function Invoke-Checked([string]$description, [string]$executable, [string[]]$arguments) {
    & $executable @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$description a échoué (code $LASTEXITCODE)."
    }
}

# --- Version -----------------------------------------------------------------------------------------------

[xml]$props = Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props') -Encoding UTF8
$versionNode = $props.SelectSingleNode('/Project/PropertyGroup/Version')
if (-not $versionNode -or -not ($versionNode.InnerText -match '^\d+\.\d+\.\d+$')) {
    throw "Directory.Build.props doit contenir <Version> au format 1.2.3."
}

$version = $versionNode.InnerText
$setup = Join-Path $dist "Stokbox-Setup-$version.exe"

Write-Host "Stokbox $version" -ForegroundColor Green

# --- Prérequis : tout ce qui manque est annoncé d'un coup, avant de lancer un build de plusieurs minutes ------

Write-Step 'Vérification des prérequis'

$problems = @()

$dotnet = Find-Dotnet
if (-not $dotnet) {
    $problems += "SDK .NET introuvable (commande dotnet). Installez le SDK .NET 8."
}

$iscc = Find-InnoSetupCompiler
if (-not $iscc) {
    $problems += "Inno Setup 6 introuvable (ISCC.exe). Installez Inno Setup 6 depuis https://jrsoftware.org/isdl.php."
}

if (-not (Test-Path -LiteralPath $iconSource) -and -not (Test-Path -LiteralPath $icon)) {
    $problems += "Icône introuvable : placez assets\icon.png (carré, 1024 px) ; assets\stokbox.ico en sera généré."
}

if (-not (Test-Path -LiteralPath $logo)) {
    $problems += "Logo introuvable : placez assets\logo.png (fond transparent) ; il s'affiche sur l'écran de connexion."
}

if (-not $WithoutDotNetRedist -and -not (Test-Path -LiteralPath $redist)) {
    $problems += "Programme d'installation de .NET Framework 4.8 introuvable : placez ndp48-x86-x64-allos-enu.exe dans installer\redist\ (voir LISEZMOI.txt), ou relancez avec -WithoutDotNetRedist pour un essai."
}

if ($problems.Count -gt 0) {
    $problems | ForEach-Object { Write-Host " - $_" -ForegroundColor Red }
    throw "Prérequis manquants : $($problems.Count)."
}

Write-Host " dotnet     : $dotnet"
Write-Host " Inno Setup : $iscc"

# --- Icône --------------------------------------------------------------------------------------------------

if (Test-Path -LiteralPath $iconSource) {
    $iconIsStale = -not (Test-Path -LiteralPath $icon) -or
        ((Get-Item -LiteralPath $icon).LastWriteTimeUtc -lt (Get-Item -LiteralPath $iconSource).LastWriteTimeUtc)
    if ($iconIsStale) {
        Write-Step "Génération de l'icône"
        & (Join-Path $root 'tools\make-icon.ps1') -Source $iconSource -Destination $icon
    }
}

# --- Build --------------------------------------------------------------------------------------------------

Write-Step 'Compilation (Release)'

# Dossier de sortie vidé d'abord : l'installateur embarque tout ce qu'il contient, il ne doit rien rester d'un ancien build.
if (Test-Path -LiteralPath $appOutput) {
    Remove-Item -LiteralPath $appOutput -Recurse -Force
}

Invoke-Checked 'La compilation' $dotnet @('build', $solution, '-c', 'Release', '-nologo', '-v', 'minimal')

# --- Tests --------------------------------------------------------------------------------------------------

Write-Step 'Tests'

Invoke-Checked 'Les tests' $dotnet @('test', $solution, '-c', 'Release', '--no-build', '-nologo', '-v', 'minimal')

# --- Contrôle de ce qui sera embarqué ------------------------------------------------------------------------

Write-Step 'Contrôle des fichiers à installer'

foreach ($required in 'Stokbox.exe', 'Stokbox.exe.config', 'Stokbox.Core.dll', 'Stokbox.Data.dll', 'System.Data.SQLite.dll',
                      'x86\SQLite.Interop.dll', 'x64\SQLite.Interop.dll') {
    if (-not (Test-Path -LiteralPath (Join-Path $appOutput $required))) {
        throw "Fichier manquant dans le build Release : $required."
    }
}

$builtVersion = (Get-Item -LiteralPath (Join-Path $appOutput 'Stokbox.exe')).VersionInfo.ProductVersion
if ($builtVersion -ne $version) {
    throw "Stokbox.exe porte la version $builtVersion au lieu de $version."
}

Write-Host " Stokbox.exe $builtVersion, SQLite natif x86 et x64 présents."

# --- Installateur -------------------------------------------------------------------------------------------

Write-Step "Compilation de l'installateur"

if (-not (Test-Path -LiteralPath $dist)) {
    New-Item -ItemType Directory -Path $dist | Out-Null
}

if (Test-Path -LiteralPath $setup) {
    Remove-Item -LiteralPath $setup -Force
}

$isccArguments = @('/Qp', "/DAppVersion=$version")
if (-not $WithoutDotNetRedist) {
    $isccArguments += '/DWithDotNetRedist'
}

$isccArguments += $installerScript
Invoke-Checked "La compilation de l'installateur" $iscc $isccArguments

if (-not (Test-Path -LiteralPath $setup)) {
    throw "L'installateur attendu n'a pas été produit : $setup."
}

$setupFile = Get-Item -LiteralPath $setup
$hash = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash

Write-Host ''
Write-Host "Installateur produit : $($setupFile.FullName)" -ForegroundColor Green
Write-Host (" Taille  : {0:N1} Mo" -f ($setupFile.Length / 1MB))
Write-Host " SHA-256 : $hash"
if ($WithoutDotNetRedist) {
    Write-Host " Attention : cet installateur n'embarque pas .NET Framework 4.8 (essai uniquement)." -ForegroundColor Yellow
}
