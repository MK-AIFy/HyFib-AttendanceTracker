# AttendTrack Enterprise — Windows restore script (Gap 14)
# Phase 5 deliverable. Run manually, interactively, by an operator.
#
# Mirrors scripts/restore.sh:
#   - Takes a .sql.gz produced by backup.ps1 / backup.sh
#   - WARNING + "Type YES to confirm" guard before touching anything
#   - dropdb --if-exists, createdb, then restores the plain-SQL dump via psql
#
# Usage (elevated PowerShell):
#   .\restore.ps1 -BackupFile 'C:\AttendTrack\backups\daily\attendtrack_20260101_020000.sql.gz' `
#                  -PgPassword 'StrongP@ss!'

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string]$BackupFile,
    [Parameter(Mandatory = $true)] [string]$PgPassword,
    [string]$PgUser = 'attendtrack',
    [string]$PgDatabase = 'attendtrack',
    [string]$PgHost = 'localhost',
    [int]$PgPort = 5432,
    [string]$PgBinPath = 'C:\Program Files\PostgreSQL\16\bin',

    # Bypasses the "Type YES" prompt — only for scripted disaster-recovery drills
    # where the operator has already confirmed out-of-band. Never set this for an
    # interactive restore.
    [switch]$SkipConfirmation
)

$ErrorActionPreference = 'Stop'

$dropDbPath = Join-Path $PgBinPath 'dropdb.exe'
$createDbPath = Join-Path $PgBinPath 'createdb.exe'
$psqlPath = Join-Path $PgBinPath 'psql.exe'

foreach ($tool in @($dropDbPath, $createDbPath, $psqlPath)) {
    if (-not (Test-Path $tool)) {
        throw "$tool not found. Pass -PgBinPath if PostgreSQL is installed elsewhere."
    }
}

if (-not (Test-Path $BackupFile)) {
    throw "Backup file not found: $BackupFile"
}

if (-not $SkipConfirmation) {
    Write-Host "WARNING: This will DESTROY and recreate the database '$PgDatabase' on $PgHost`:$PgPort." -ForegroundColor Yellow
    $confirm = Read-Host 'Type YES to confirm'
    if ($confirm -ne 'YES') {
        Write-Host 'Aborted.' -ForegroundColor Cyan
        exit 0
    }
}

# Decompress the .sql.gz produced by backup.ps1 / backup.sh into a temp .sql file
$tempSql = Join-Path ([System.IO.Path]::GetTempPath()) "attendtrack_restore_$([guid]::NewGuid()).sql"
Write-Host "Decompressing $BackupFile..." -ForegroundColor Cyan
$inStream = [System.IO.File]::OpenRead($BackupFile)
$gzip = New-Object System.IO.Compression.GZipStream($inStream, [System.IO.Compression.CompressionMode]::Decompress)
$outStream = [System.IO.File]::Create($tempSql)
try {
    $gzip.CopyTo($outStream)
} finally {
    $outStream.Dispose(); $gzip.Dispose(); $inStream.Dispose()
}

$env:PGPASSWORD = $PgPassword
try {
    Write-Host "Restoring from $BackupFile..." -ForegroundColor Cyan

    & $dropDbPath -h $PgHost -p $PgPort -U $PgUser --if-exists $PgDatabase
    if ($LASTEXITCODE -ne 0) { throw "dropdb exited with code $LASTEXITCODE" }

    & $createDbPath -h $PgHost -p $PgPort -U $PgUser $PgDatabase
    if ($LASTEXITCODE -ne 0) { throw "createdb exited with code $LASTEXITCODE" }

    & $psqlPath -h $PgHost -p $PgPort -U $PgUser -d $PgDatabase -f $tempSql
    if ($LASTEXITCODE -ne 0) { throw "psql restore exited with code $LASTEXITCODE" }
} finally {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    Remove-Item $tempSql -Force -ErrorAction SilentlyContinue
}

Write-Host 'Restore complete.' -ForegroundColor Green
