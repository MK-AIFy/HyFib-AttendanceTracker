# AttendTrack Enterprise — Windows health check
# Phase 5 deliverable. Schedule every 5 minutes via Task Scheduler.
#
# - Probes /health on the local site
# - On non-200 OR consecutive failures, optionally sends email via SMTP
# - Logs to <InstallDir>\logs\healthcheck.log
#
# Usage:
#   .\healthcheck.ps1 -HealthUrl 'https://localhost/health' -InstallDir 'C:\AttendTrack'
#   .\healthcheck.ps1 -HealthUrl 'https://attendtrack.local/health' `
#                     -SmtpServer 'smtp.local' -SmtpFrom 'ops@x.in' -SmtpTo 'admin@x.in'

[CmdletBinding()]
param(
    [string]$HealthUrl = 'https://localhost/health',
    [string]$InstallDir = 'C:\AttendTrack',
    [int]$TimeoutSeconds = 10,

    [string]$SmtpServer,
    [int]$SmtpPort = 25,
    [string]$SmtpFrom,
    [string]$SmtpTo,
    [pscredential]$SmtpCredential,
    [switch]$SmtpUseSsl,

    # If TLS cert is self-signed in lab/dev, allow it
    [switch]$IgnoreCertificateErrors
)

$ErrorActionPreference = 'Continue'

$logDir = Join-Path $InstallDir 'logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$logFile = Join-Path $logDir 'healthcheck.log'

function Write-LogLine {
    param([string]$Level, [string]$Message)
    $line = '{0:yyyy-MM-dd HH:mm:ss}  [{1}]  {2}' -f (Get-Date), $Level, $Message
    Add-Content -Path $logFile -Value $line
    Write-Host $line
}

if ($IgnoreCertificateErrors) {
    [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
}

$status = 'Unknown'
$detail = ''
$ok = $false
try {
    $resp = Invoke-WebRequest -Uri $HealthUrl -UseBasicParsing -TimeoutSec $TimeoutSeconds -ErrorAction Stop
    $status = [int]$resp.StatusCode
    $detail = ($resp.Content -replace '\s+', ' ').Trim()
    if ($resp.StatusCode -eq 200) {
        $ok = $true
        Write-LogLine 'OK' "$HealthUrl -> 200"
    } else {
        Write-LogLine 'WARN' "$HealthUrl -> $status $detail"
    }
} catch {
    $status = 'ERROR'
    $detail = $_.Exception.Message
    Write-LogLine 'ERROR' "$HealthUrl unreachable: $detail"
}

if (-not $ok -and $SmtpServer -and $SmtpFrom -and $SmtpTo) {
    try {
        $subject = "[AttendTrack] Health check FAILED on $env:COMPUTERNAME"
        $body = @"
Health check failed for AttendTrack.

URL    : $HealthUrl
Status : $status
Detail : $detail
Host   : $env:COMPUTERNAME
Time   : $(Get-Date -Format o)

Logs   : $logFile
"@
        $params = @{
            SmtpServer = $SmtpServer
            Port = $SmtpPort
            From = $SmtpFrom
            To = $SmtpTo
            Subject = $subject
            Body = $body
            UseSsl = [bool]$SmtpUseSsl
        }
        if ($SmtpCredential) { $params.Credential = $SmtpCredential }
        Send-MailMessage @params -ErrorAction Stop
        Write-LogLine 'ALERT' "Email sent to $SmtpTo"
    } catch {
        Write-LogLine 'ERROR' "Failed to send alert email: $($_.Exception.Message)"
    }
}

if (-not $ok) { exit 1 } else { exit 0 }
