<#
Start-and-check integration script

This script will:
- create a `logs` folder
- start the backend (`dotnet run`) and stream logs to `logs/backend.log` and `logs/backend.err`
- start the frontend (`npm run dev`) and stream logs to `logs/frontend.log` and `logs/frontend.err`
- wait until backend (`/api/scraper/status`) and frontend (`/`) respond
- POST to `/api/scraper/refresh` to trigger the scraper
- (optional) start the scraper directly and capture `logs/scraper.log`

Run from repo root (or execute this file directly):
  powershell -ExecutionPolicy Bypass -File .\scripts\start_and_check.ps1

#>

Set-StrictMode -Version Latest

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
# Assume repository root is the parent of the scripts directory
$root = Resolve-Path (Join-Path $scriptDir '..') | Select-Object -ExpandProperty Path

$logs = Join-Path $root 'logs'
New-Item -Path $logs -ItemType Directory -Force | Out-Null

$backendLog = Join-Path $logs 'backend.log'
$backendErr = Join-Path $logs 'backend.err'
$frontendLog = Join-Path $logs 'frontend.log'
$frontendErr = Join-Path $logs 'frontend.err'
$scraperLog = Join-Path $logs 'scraper.log'
$scraperErr = Join-Path $logs 'scraper.err'
$refreshRespFile = Join-Path $logs 'refresh_response.json'

Write-Host "Logs -> $logs"

function Wait-For-Url {
    param(
        [string]$Url,
        [int]$TimeoutSec = 60
    )
    $start = Get-Date
    while ((Get-Date) -lt $start.AddSeconds($TimeoutSec)) {
        try {
            $r = Invoke-RestMethod -Method Get -Uri $Url -ErrorAction Stop
            return $true
        } catch {
            Start-Sleep -Seconds 1
        }
    }
    return $false
}

Write-Host "First the backend (dotnet run)..."
$backendProj = Join-Path $root 'backend\NewsAggregator.Api'
if (-not (Test-Path $backendProj)) { Write-Host "Backend project not found at $backendProj" -ForegroundColor Red }
$backendArgs = @('run','--project',$backendProj,'--configuration','Debug')
$backendProc = Start-Process -FilePath 'dotnet' -ArgumentList $backendArgs -WorkingDirectory $root -RedirectStandardOutput $backendLog -RedirectStandardError $backendErr -PassThru

Write-Host "Waiting for backend (/api/scraper/status) to be available (60s)..."
$backendReady = Wait-For-Url -Url 'http://localhost:5000/api/scraper/status' -TimeoutSec 60
if (-not $backendReady) {
    Write-Host "Backend not reachable at http://localhost:5000 (check logs: $backendLog, $backendErr)" -ForegroundColor Red
} else {
    Write-Host "Backend is up."
}

Write-Host "Second the frontend (npm run dev)..."
$frontendDir = Join-Path $root 'frontend'
if (-not (Test-Path $frontendDir)) { Write-Host "Frontend dir not found at $frontendDir" -ForegroundColor Yellow }
$npmArgs = @('run','dev')
try {
    # Use npm.cmd on Windows; fall back to npx.cmd if npm doesnt work
    $npmCmd = 'npm.cmd'
    if (-not (Get-Command $npmCmd -ErrorAction SilentlyContinue)) { $npmCmd = 'npx.cmd' }
    $frontendProc = Start-Process -FilePath $npmCmd -ArgumentList $npmArgs -WorkingDirectory $frontendDir -RedirectStandardOutput $frontendLog -RedirectStandardError $frontendErr -PassThru
} catch {
    Write-Host "Failed to start frontend: $_" -ForegroundColor Yellow
}

Write-Host "Waiting for frontend (http://localhost:3000) to be available (60s)..."
$frontendReady = Wait-For-Url -Url 'http://localhost:3000' -TimeoutSec 60
if (-not $frontendReady) {
    Write-Host "Frontend not reachable at http://localhost:3000 (check logs: $frontendLog, $frontendErr)" -ForegroundColor Yellow
} else {
    Write-Host "Frontend is up."
}

Write-Host "Triggering scraper refresh via backend API..."
try {
    $resp = Invoke-RestMethod -Method Post -Uri 'http://localhost:5000/api/scraper/refresh' -TimeoutSec 10 -ErrorAction Stop
    $resp | ConvertTo-Json -Depth 5 | Out-File -FilePath $refreshRespFile -Encoding UTF8
    Write-Host "Refresh triggered; response saved to $refreshRespFile"
} catch {
    Write-Host "Failed to call refresh endpoint: $_" -ForegroundColor Yellow
}

# Optionally run the scraper directly (this will POST to backend as usual)
Write-Host "Starting scraper directly (py -3 main.py) and logging to $scraperLog"
$scraperDir = Join-Path $root 'scraper'
if (-not (Test-Path $scraperDir)) { Write-Host "Scraper dir not found at $scraperDir" -ForegroundColor Yellow }
try {
    $pyArgs = @('-3','main.py')
    $scraperProc = Start-Process -FilePath 'py' -ArgumentList $pyArgs -WorkingDirectory $scraperDir -RedirectStandardOutput $scraperLog -RedirectStandardError $scraperErr -PassThru
} catch {
    Write-Host "Failed to start scraper process directly: $_" -ForegroundColor Yellow
}

Write-Host "Waiting 10 seconds for scraper activity..."
Start-Sleep -Seconds 10

Write-Host "--- Summary ---"
Write-Host "Backend ready: $backendReady"
Write-Host "Frontend ready: $frontendReady"
if (Test-Path $refreshRespFile) { Get-Content $refreshRespFile -Raw | Write-Host }

Write-Host "Tail of backend log (last 200 lines):"
if (Test-Path $backendLog) { Get-Content $backendLog -Tail 200 | Out-String | Write-Host }

Write-Host "Tail of frontend log (last 200 lines):"
if (Test-Path $frontendLog) { Get-Content $frontendLog -Tail 200 | Out-String | Write-Host }

Write-Host "Tail of scraper log (last 200 lines):"
if (Test-Path $scraperLog) { Get-Content $scraperLog -Tail 200 | Out-String | Write-Host }

Write-Host "Script finished. Logs are in: $logs"
