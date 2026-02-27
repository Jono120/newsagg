# Deployment Runbook

Short guide to configure secrets and perform deployments for the NewsAggregator project.

Required GitHub secrets (used by workflows in `.github/workflows/`):
- `AZURE_CREDENTIALS` — service principal JSON for `azure/login` (see `az ad sp create-for-rbac --sdk-auth`).
- `AZURE_RESOURCE_GROUP` — target resource group name for IaC deploys.
- `AZURE_WEBAPP_NAME` — name of the App Service created (or to be created) for the backend.
- `AZURE_STORAGE_ACCOUNT` — storage account name used for static website hosting.
- `AZURE_STORAGE_KEY` — primary key for the storage account (used by the frontend deploy workflow).
- `ACR_LOGIN_SERVER` — ACR login server (e.g. `myacr.azurecr.io`).
- `ACR_USERNAME` and `ACR_PASSWORD` — credentials for pushing to ACR (or use `azure/docker-login` with service principal).
- `AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET`, `AZURE_TENANT_ID` — (optional) service principal creds used in some scripts.
- `CONTAINERAPP_NAME` — name of the Container App to update (used by `scraper-container` workflow).

How secrets are used
- The IaC template (`infra/main.bicep`) outputs ACR login server, App Insights key and Web App URL. The template also wires Cosmos DB endpoint and key into the backend App's app settings.
- CI workflows read `AZURE_CREDENTIALS` to perform `az` operations and use the ACR secrets to build/push images.

Granting ACR pull to the scraper identity (optional, recommended instead of placing registry creds in secrets)
1. Find AcrPull role definition id:

```powershell
az role definition list --name AcrPull --query '[].{Name:roleName,Id:roleDefinitionId}' -o table
```

2. Assign role to the user-assigned identity principal id (substitute real values):

```powershell
az role assignment create \
  --assignee-object-id <IDENTITY_PRINCIPAL_ID> \
  --assignee-principal-type ServicePrincipal \
  --role AcrPull \
  --scope /subscriptions/<SUB>/resourceGroups/<RG>/providers/Microsoft.ContainerRegistry/registries/<ACR_NAME>
```

Deploy IaC (example)

```powershell
az deployment group create \
  --resource-group MyResourceGroup \
  --template-file infra/main.bicep \
  --parameters prefix=newsagg environment=staging \
  --parameters acrPullRoleDefinitionId='<ACR_PULL_GUID>'
```

Notes and next steps
- After IaC deploy, populate GitHub Secrets with values printed by the deployment (ACR login server, storage account key, App URL if needed).
- Consider enabling managed identity access to ACR (AcrPull) to avoid storing registry credentials.
- Add monitoring and alerting in Azure Monitor for Cosmos DB RU usage and backend errors.
After IaC deploy, populate GitHub Secrets with values printed by the deployment (ACR login server, storage account key, App URL if needed).

Local validation helper

A small PowerShell helper script is included at `infra/validate-iac.ps1` to run a local Bicep build and optionally validate the template against an existing resource group.

Basic usage (only build):

```powershell
# Build only (no Azure CLI validate)
.\infra\validate-iac.ps1
```

Validate against an existing resource group (attempts to auto-discover the `AcrPull` roleDefinitionId if not provided):

```powershell
.\infra\validate-iac.ps1 -ResourceGroup MyResourceGroup

# or explicitly provide AcrPull role definition id
.\infra\validate-iac.ps1 -ResourceGroup MyResourceGroup -AcrPullRoleDefinitionId <ACR_PULL_GUID>
```
