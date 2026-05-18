<#
Start the containerized solution using Docker Compose.

This brings up PostgreSQL, the backend API, the frontend, and the scraper worker.
#>

Set-StrictMode -Version Latest

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$root = Resolve-Path (Join-Path $scriptDir '..') | Select-Object -ExpandProperty Path

if (-not (Get-Command 'docker' -ErrorAction SilentlyContinue)) {
    throw 'Docker is not installed or not on PATH.'
}

Write-Host 'Starting the News Aggregator stack with Docker Compose...'
docker compose -f (Join-Path $root 'docker-compose.yml') up -d --build

Write-Host ''
Write-Host 'Stack started.'
Write-Host 'Frontend: http://localhost:3000'
Write-Host 'Backend: http://localhost:5000'
Write-Host 'PostgreSQL: localhost:5432'