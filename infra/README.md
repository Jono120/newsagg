# Infra / IaC

This folder contains a minimal Bicep skeleton to provision core resources for the NewsAggregator app.

Included resources (skeleton):
- Azure Container Registry (ACR)
- Azure Cosmos DB (SQL API) with `NewsAggregatorDb` and `Articles` container (partition key `/source`)
- Application Insights
-- (No Key Vault managed by this template)
- App Service Plan + Web App (Linux) for the backend
- Storage account configured for static website hosting (frontend)

Quick deploy (resource group must already exist):

## Infra / IaC

This folder contains a Bicep skeleton to provision core resources for the NewsAggregator app.

Included resources (skeleton):
- Azure Container Registry (ACR)
- Azure Cosmos DB (SQL API) with `NewsAggregatorDb` and `Articles` container (partition key `/source`)
- Application Insights
-- (No Key Vault managed by this template)
- App Service Plan + Web App (Linux) for the backend
- Storage account configured for static website hosting (frontend)
- Log Analytics workspace and Container Apps managed environment
- Container App skeleton for the `scraper/` image

Quick deploy (resource group must already exist):

```powershell
az deployment group create \
  --resource-group MyResourceGroup \
  --template-file infra/main.bicep \
  --parameters prefix=newsagg environment=staging
```

- Notes / next steps:
- The Bicep template creates two user-assigned managed identities:
  - one for the Web App
  - one for the Scraper Container App
- The Container App skeleton uses an image at `${acrLoginServer}/newsagg/scraper:latest` and leaves registry credentials as placeholders. Recommended flows:
  - CI builds and pushes scraper image to ACR and stores ACR credentials in GitHub Secrets.
  - Or grant the scraper identity the `AcrPull` role on the ACR after deployment (role assignment step).
- CI/CD: add GitHub Actions to build/publish images to ACR, deploy the backend to App Service, and upload frontend artifacts to the static storage account.


Role assignment notes
- This template supports a role assignment parameter for `AcrPull` so you can grant the scraper identity pull access to ACR. To find the AcrPull role definition GUID run:

```powershell
# Find AcrPull role GUID
az role definition list --name AcrPull --query '[].{Name:roleName,Id:roleDefinitionId}' -o table
```

Example deploy (passes AcrPull role definition ID):

```powershell
az deployment group create \
  --resource-group MyResourceGroup \
  --template-file infra/main.bicep \
  --parameters prefix=newsagg environment=staging \
  --parameters acrPullRoleDefinitionId='<ACR_PULL_GUID>'
```


If you want, I can now:
- Add a Bicep role assignment resource for `AcrPull` so the scraper identity can pull images from ACR, or
- Generate GitHub Actions workflows to build/push images and deploy the IaC and apps.
