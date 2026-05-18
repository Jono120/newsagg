<#
Provision and deploy the Azure hosting stack for the News Aggregator using azd.

This script uses the azd project manifest and the Bicep template under infra/.

Prerequisites:
- azd installed and logged in
- Azure subscription available
- PostgreSQL admin password provided
#>

[CmdletBinding()]
param(
    [string]$EnvironmentName = 'newsagg-payg',

    [string]$Location = 'newzealandnorth',

    [string]$Subscription = '9dbcb279-5b37-4903-88c2-6572c7286b0e',

    [string]$Prefix = 'newsagg',

    [string]$PostgresAdminUsername = 'pgadmin',

    [Parameter(Mandatory = $true)]
    [string]$PostgresAdminPassword
)

Set-StrictMode -Version Latest

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$root = Resolve-Path (Join-Path $scriptDir '..') | Select-Object -ExpandProperty Path

if (-not (Get-Command 'azd' -ErrorAction SilentlyContinue)) {
    throw 'Azure Developer CLI (azd) is not installed or not on PATH.'
}

Push-Location $root
try {
    azd env new $EnvironmentName --no-prompt | Out-Host
    azd env set AZURE_SUBSCRIPTION_ID $Subscription --no-prompt | Out-Host
    azd env set AZURE_LOCATION $Location --no-prompt | Out-Host
    azd env config set infra.parameters.prefix $Prefix --environment $EnvironmentName | Out-Host
    azd env config set infra.parameters.postgresAdminUsername $PostgresAdminUsername --environment $EnvironmentName | Out-Host
    azd env config set infra.parameters.postgresAdminPassword $PostgresAdminPassword --environment $EnvironmentName | Out-Host

    azd provision --preview --environment $EnvironmentName --no-prompt | Out-Host
    azd up --environment $EnvironmentName --no-prompt | Out-Host
}
finally {
    Pop-Location
}

Write-Host ''
Write-Host 'Azure infrastructure deployed.'
