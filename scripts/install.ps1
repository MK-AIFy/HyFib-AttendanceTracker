# AttendTrack Enterprise — Windows Server install script
# Phase 5 deliverable. Runs as Administrator.
#
# Installs / verifies:
#   - Chocolatey (package manager)
#   - PostgreSQL 16
#   - Memurai Developer (Redis-compatible for Windows)
#   - .NET 9 ASP.NET Core Hosting Bundle (in-process IIS hosting)
#   - IIS + ASP.NET Core Module v2 (AspNetCoreModuleV2)
#
# Usage (elevated PowerShell):
#   Set-ExecutionPolicy -Scope Process Bypass -Force
#   .\install.ps1 -PostgresPassword 'StrongP@ss!' -InstallDir 'C:\AttendTrack'

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PostgresPassword,

    [string]$InstallDir = 'C:\AttendTrack',

    [string]$AppPoolName = 'AttendTrack',

    [string]$SiteName = 'AttendTrack',

    [int]$SitePort = 443,

    [switch]$SkipChocolatey,

    [switch]$SkipPostgres,

    [switch]$SkipRedis,

    [switch]$SkipDotnet,

    [switch]$SkipIis
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Test-Admin {
    $current = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($current)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Admin)) {
    throw 'install.ps1 must be run from an elevated PowerShell session (Run as Administrator).'
}

Write-Host '== AttendTrack install ==' -ForegroundColor Cyan
Write-Host ('Install dir : {0}' -f $InstallDir)
Write-Host ('AppPool     : {0}' -f $AppPoolName)
Write-Host ('Site        : {0} on port {1}' -f $SiteName, $SitePort)

# 1. Chocolatey ----------------------------------------------------------------
if (-not $SkipChocolatey) {
    if (-not (Get-Command choco -ErrorAction SilentlyContinue)) {
        Write-Host '[1/5] Installing Chocolatey...' -ForegroundColor Yellow
        Set-ExecutionPolicy -Scope Process Bypass -Force
        [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
        Invoke-Expression ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))
        # Refresh PATH for this session
        $env:Path = [System.Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [System.Environment]::GetEnvironmentVariable('Path', 'User')
    } else {
        Write-Host '[1/5] Chocolatey already installed.' -ForegroundColor Green
    }
}

# 2. PostgreSQL ----------------------------------------------------------------
if (-not $SkipPostgres) {
    Write-Host '[2/5] Installing PostgreSQL 16...' -ForegroundColor Yellow
    $pgService = Get-Service -Name 'postgresql-x64-16' -ErrorAction SilentlyContinue
    if ($null -eq $pgService) {
        choco install postgresql16 --params "/Password:$PostgresPassword" -y --no-progress
    } else {
        Write-Host '  PostgreSQL 16 service already present.' -ForegroundColor Green
    }
}

# 3. Memurai (Redis for Windows) -----------------------------------------------
if (-not $SkipRedis) {
    Write-Host '[3/5] Installing Memurai Developer (Redis-compatible)...' -ForegroundColor Yellow
    $memurai = Get-Service -Name 'Memurai' -ErrorAction SilentlyContinue
    if ($null -eq $memurai) {
        choco install memurai-developer -y --no-progress
    } else {
        Write-Host '  Memurai service already present.' -ForegroundColor Green
    }
}

# 4. .NET 9 Hosting Bundle -----------------------------------------------------
if (-not $SkipDotnet) {
    Write-Host '[4/5] Installing .NET 9 ASP.NET Core Hosting Bundle...' -ForegroundColor Yellow
    # dotnet-9.0-windowshosting installs the Hosting Bundle (runtime + ASP.NET Core Module v2)
    choco install dotnet-9.0-windowshosting -y --no-progress
}

# 5. IIS + AspNetCoreModuleV2 --------------------------------------------------
if (-not $SkipIis) {
    Write-Host '[5/5] Enabling IIS features...' -ForegroundColor Yellow
    $features = @(
        'IIS-WebServerRole',
        'IIS-WebServer',
        'IIS-CommonHttpFeatures',
        'IIS-StaticContent',
        'IIS-DefaultDocument',
        'IIS-HttpErrors',
        'IIS-HttpRedirect',
        'IIS-ApplicationDevelopment',
        'IIS-NetFxExtensibility45',
        'IIS-HealthAndDiagnostics',
        'IIS-HttpLogging',
        'IIS-Security',
        'IIS-RequestFiltering',
        'IIS-Performance',
        'IIS-WebServerManagementTools',
        'IIS-ManagementConsole',
        'IIS-IIS6ManagementCompatibility',
        'IIS-Metabase',
        'IIS-WebSockets'
    )
    foreach ($f in $features) {
        $state = (Get-WindowsOptionalFeature -Online -FeatureName $f -ErrorAction SilentlyContinue).State
        if ($state -ne 'Enabled') {
            Enable-WindowsOptionalFeature -Online -FeatureName $f -All -NoRestart | Out-Null
        }
    }

    Import-Module WebAdministration

    # Restart IIS so AspNetCoreModuleV2 (just installed) is registered
    iisreset /restart | Out-Null

    # Create app pool (no managed runtime — Kestrel hosts in-process)
    if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) {
        New-WebAppPool -Name $AppPoolName | Out-Null
        Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name 'managedRuntimeVersion' -Value ''
        Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name 'startMode' -Value 'AlwaysRunning'
    }

    # Site directory + placeholder
    $sitePath = Join-Path $InstallDir 'web'
    New-Item -ItemType Directory -Path $sitePath -Force | Out-Null
    if (-not (Get-Website -Name $SiteName -ErrorAction SilentlyContinue)) {
        New-Website -Name $SiteName -PhysicalPath $sitePath -ApplicationPool $AppPoolName -Port $SitePort -Ssl | Out-Null
        Write-Host ("  IIS site '{0}' created at {1} (port {2}). Bind a TLS certificate in IIS Manager." -f $SiteName, $sitePath, $SitePort) -ForegroundColor Green
    }

    # Create supporting directories
    foreach ($sub in 'logs', 'face-captures', 'backups\daily', 'backups\weekly') {
        New-Item -ItemType Directory -Path (Join-Path $InstallDir $sub) -Force | Out-Null
    }
}

Write-Host ''
Write-Host '== Install complete ==' -ForegroundColor Cyan
Write-Host 'Next steps:'
Write-Host '  1. Copy your published web build to' (Join-Path $InstallDir 'web')
Write-Host '  2. Copy appsettings.Production.template.json -> appsettings.Production.json and fill secrets'
Write-Host '  3. Bind an HTTPS certificate to the site in IIS Manager'
Write-Host '  4. Schedule scripts\backup.ps1 daily and scripts\healthcheck.ps1 every 5 minutes'
