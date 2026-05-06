# AttendTrack Enterprise — Windows backup script
# Phase 5 deliverable. Schedule daily via Task Scheduler.
#
# Mirrors scripts/backup.sh:
#   - pg_dump -> backups\daily (gzip)
#   - face-captures tarball -> backups\daily
#   - On Sundays: copy of the day's SQL dump -> backups\weekly
#   - Rotation: 30 days of dailies, 84 days (12 weeks) of weeklies
#
# Usage (elevated PowerShell or scheduled task):
#   .\backup.ps1 `
#     -InstallDir 'C:\AttendTrack' `
#     -PgUser 'attendtrack' `
#     -PgDatabase 'attendtrack' `
#     -PgPassword 'StrongP@ss!'

[CmdletBinding()]
param(
    [string]$InstallDir = 'C:\AttendTrack',
    [string]$PgUser = 'attendtrack',
    [string]$PgDatabase = 'attendtrack',
    [Parameter(Mandatory = $true)] [string]$PgPassword,
    [string]$PgHost = 'localhost',
    [int]$PgPort = 5432,
    [string]$PgDumpPath = 'C:\Program Files\PostgreSQL\16\bin\pg_dump.exe'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $PgDumpPath)) {
    throw "pg_dump.exe not found at $PgDumpPath. Pass -PgDumpPath if PostgreSQL is installed elsewhere."
}

$dailyDir = Join-Path $InstallDir 'backups\daily'
$weeklyDir = Join-Path $InstallDir 'backups\weekly'
$faceDir = Join-Path $InstallDir 'face-captures'

New-Item -ItemType Directory -Force -Path $dailyDir, $weeklyDir | Out-Null

$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$sqlPath = Join-Path $dailyDir "attendtrack_${stamp}.sql"
$gzPath = "$sqlPath.gz"
$facePath = Join-Path $dailyDir "face-captures_${stamp}.zip"

Write-Host "[$stamp] Starting backup..." -ForegroundColor Cyan

# 1. pg_dump (set PGPASSWORD env so pg_dump won't prompt)
$env:PGPASSWORD = $PgPassword
try {
    & $PgDumpPath -h $PgHost -p $PgPort -U $PgUser -d $PgDatabase -F p -f $sqlPath
    if ($LASTEXITCODE -ne 0) {
        throw "pg_dump exited with code $LASTEXITCODE"
    }
} finally {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
}

# Gzip the SQL dump (PowerShell 5+ has Compress-Archive but it's zip; use .NET GZip for parity with Linux .sql.gz)
Add-Type -AssemblyName System.IO.Compression
$inStream = [System.IO.File]::OpenRead($sqlPath)
$outStream = [System.IO.File]::Create($gzPath)
$gzip = New-Object System.IO.Compression.GZipStream($outStream, [System.IO.Compression.CompressionMode]::Compress)
try {
    $inStream.CopyTo($gzip)
} finally {
    $gzip.Dispose(); $outStream.Dispose(); $inStream.Dispose()
}
Remove-Item $sqlPath -Force

# 2. Face captures zip (zip is the closest Windows-native equivalent to tar.gz)
if (Test-Path $faceDir) {
    Compress-Archive -Path (Join-Path $faceDir '*') -DestinationPath $facePath -Force -ErrorAction SilentlyContinue
}

# 3. Weekly copy on Sunday
if ((Get-Date).DayOfWeek -eq 'Sunday') {
    Copy-Item $gzPath -Destination $weeklyDir -Force
}

# 4. Rotation
$now = Get-Date
Get-ChildItem $dailyDir -File -Filter '*.gz', '*.zip' -ErrorAction SilentlyContinue |
    Where-Object { $now - $_.LastWriteTime -gt [TimeSpan]::FromDays(30) } |
    Remove-Item -Force -ErrorAction SilentlyContinue

Get-ChildItem $weeklyDir -File -Filter '*.gz' -ErrorAction SilentlyContinue |
    Where-Object { $now - $_.LastWriteTime -gt [TimeSpan]::FromDays(84) } |
    Remove-Item -Force -ErrorAction SilentlyContinue

Write-Host "[$stamp] Backup complete: $gzPath" -ForegroundColor Green
