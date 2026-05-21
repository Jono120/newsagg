<#
Provision and deploy the Azure hosting stack for the News Aggregator using azd.

This script uses the azd project manifest and the Bicep template under infra/.

Prerequisites:
- azd installed and logged in
- Azure subscription available (az login)
- PostgreSQL admin password provided
#>

[CmdletBinding()]
param(
    [string]$EnvironmentName = 'newsagg-prod',

    [string]$Location = 'newzealandnorth',

    [string]$Subscription,

    [string]$Prefix = 'newsagg',

    [string]$PostgresAdminUsername = 'pgadmin',

    [switch]$ProvisionOnly,

    [Parameter(Mandatory = $true)]
    [string]$PostgresAdminPassword
)

Set-StrictMode -Version Latest

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$root = Resolve-Path (Join-Path $scriptDir '..') | Select-Object -ExpandProperty Path

if (-not (Get-Command 'azd' -ErrorAction SilentlyContinue)) {
    throw 'Azure Developer CLI (azd) is not installed or not on PATH.'
}

if (-not $Subscription) {
    if (-not (Get-Command 'az' -ErrorAction SilentlyContinue)) {
        throw 'Azure CLI (az) is not installed. Pass -Subscription or install az and run az login.'
    }
    $Subscription = az account show --query id -o tsv 2>$null
    if (-not $Subscription) {
        throw 'No Azure subscription found. Run az login or pass -Subscription.'
    }
}

Push-Location $root
try {
    azd env new $EnvironmentName --no-prompt | Out-Host
    if ($LASTEXITCODE -ne 0) {
        azd env select $EnvironmentName --no-prompt | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to create or select azd environment '$EnvironmentName'."
        }
    }
    azd env set AZURE_SUBSCRIPTION_ID $Subscription --no-prompt | Out-Host
    azd env set AZURE_LOCATION $Location --no-prompt | Out-Host
    azd env config set infra.parameters.prefix $Prefix --environment $EnvironmentName | Out-Host
    azd env config set infra.parameters.postgresAdminUsername $PostgresAdminUsername --environment $EnvironmentName | Out-Host
    azd env config set infra.parameters.postgresAdminPassword $PostgresAdminPassword --environment $EnvironmentName | Out-Host

    if ($ProvisionOnly) {
        azd provision --environment $EnvironmentName --no-prompt | Out-Host
    }
    else {
        azd up --environment $EnvironmentName --no-prompt | Out-Host
    }
}
finally {
    Pop-Location
}

Write-Host ''
if ($ProvisionOnly) {
    Write-Host 'Azure infrastructure provisioned.'
}
else {
    Write-Host 'Azure infrastructure deployed.'
}
