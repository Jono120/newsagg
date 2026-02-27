param(
  [string]$ResourceGroup,
  [string]$AcrPullRoleDefinitionId,
  [string]$Prefix = 'newsagg',
  [string]$Environment = 'staging'
)

function Ensure-AzCli {
  if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Error "Azure CLI (az) is not installed or not on PATH. Install from https://aka.ms/installazurecliwindows"
    return $false
  }
  return $true
}

if (-not (Ensure-AzCli)) { exit 2 }

Write-Host "Building Bicep template infra/main.bicep..."
az bicep build --file infra/main.bicep
if ($LASTEXITCODE -ne 0) {
  Write-Error "Bicep build failed. Fix template errors and try again."
  exit $LASTEXITCODE
}

if (-not $ResourceGroup) {
  Write-Host "No ResourceGroup provided — finished build step only. To validate deployment provide -ResourceGroup <name>."
  exit 0
}

if (-not $AcrPullRoleDefinitionId) {
  Write-Host "Attempting to discover AcrPull roleDefinitionId..."
  $acrDef = az role definition list --name AcrPull --query '[0].roleDefinitionId' -o tsv
  if ($acrDef) {
    $AcrPullRoleDefinitionId = $acrDef.Trim()
    Write-Host "Found AcrPull roleDefinitionId: $AcrPullRoleDefinitionId"
  } else {
    Write-Error "Failed to find AcrPull role definition. Provide -AcrPullRoleDefinitionId explicitly."
    exit 3
  }
}

Write-Host "Validating deployment against resource group: $ResourceGroup (this performs a validate, not a create)."
az deployment group validate --resource-group $ResourceGroup --template-file infra/main.bicep --parameters prefix=$Prefix environment=$Environment acrPullRoleDefinitionId=$AcrPullRoleDefinitionId
if ($LASTEXITCODE -ne 0) {
  Write-Error "Template validation failed. Inspect the output above for errors."
  exit $LASTEXITCODE
}

Write-Host "Validation succeeded. You can proceed to deploy with az deployment group create ..."
