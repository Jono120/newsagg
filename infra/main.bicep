// Bicep skeleton for NewsAgg core Azure resources
@description('Location for all resources')
param location string = resourceGroup().location
@description('Name prefix (short) used for resources')
param prefix string = 'newsagg'
@description('Deployment environment name (dev|staging|prod)')
param environment string = 'staging'

@description('Role definition id (GUID) for AcrPull - pass the GUID, e.g. from `az role definition list --name AcrPull`')
param acrPullRoleDefinitionId string

// Key Vault role assignment removed; manage secrets via RBAC or external process

var namePrefix = '${prefix}-${environment}'

// User-assigned Managed Identities for workloads
resource webIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' = {
  name: '${namePrefix}-web-identity'
  location: location
}

resource containerIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' = {
  name: '${namePrefix}-scraper-identity'
  location: location
}

// Application Insights
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${namePrefix}-ai'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
  }
}

// Azure Container Registry
resource acr 'Microsoft.ContainerRegistry/registries@2022-02-01' = {
  name: '${namePrefix}acr'
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {}
}

// Cosmos DB account (SQL API)
resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2021-07-01-preview' = {
  name: '${namePrefix}-cosmos'
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
  }
}

// Cosmos DB SQL Database
resource cosmosDb 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2021-07-01-preview' = {
  name: '${cosmosAccount.name}/NewsAggregatorDb'
  properties: {
    resource: {
      id: 'NewsAggregatorDb'
    }
    options: {}
  }
  dependsOn: [cosmosAccount]
}

// Cosmos DB Container (Articles) with partition key /source
resource cosmosContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2021-07-01-preview' = {
  name: '${cosmosDb.name}/Articles'
  properties: {
    resource: {
      id: 'Articles'
      partitionKey: {
        paths: [ '/source' ]
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
      }
    }
    options: {}
  }
  dependsOn: [cosmosDb]
}

// Storage account for frontend static website
resource staticStorage 'Microsoft.Storage/storageAccounts@2022-09-01' = {
  name: toLower('${namePrefix}static')
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    staticWebsite: {
      indexDocument: 'index.html'
      error404Document: 'index.html'
    }
  }
}

// App Service Plan (Linux)
resource appServicePlan 'Microsoft.Web/serverfarms@2021-02-01' = {
  name: '${namePrefix}-plan'
  location: location
  sku: {
    name: 'B1'
    tier: 'Basic'
    capacity: 1
  }
  properties: {
    reserved: true
  }
}

// Web App for backend API (dotnet 8)
resource webApp 'Microsoft.Web/sites@2021-02-01' = {
  name: '${namePrefix}-api'
  location: location
  kind: 'app,linux'
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: 'DOTNET|8.0'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: environment
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsights.properties.InstrumentationKey
        }
        {
          name: 'CosmosDb:Endpoint'
          value: cosmosAccount.properties.documentEndpoint
        }
        {
          name: 'CosmosDb:Key'
          value: listKeys(cosmosAccount.id, '2021-07-01-preview').primaryMasterKey
        }
        {
          name: 'CosmosDb:DatabaseName'
          value: 'NewsAggregatorDb'
        }
        {
          name: 'CosmosDb:ContainerName'
          value: 'Articles'
        }
      ]
    }
  }
  dependsOn: [appServicePlan, appInsights]
}

// Assign the user-assigned identity to the Web App
resource webAppIdentityAssignment 'Microsoft.Web/sites/identity@2021-02-01' = {
  name: '${webApp.name}/identity'
  properties: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${webIdentity.id}': {}
    }
  }
  dependsOn: [webApp, webIdentity]
}

// Outputs to help wiring CI/CD and app settings
output cosmosAccountName string = cosmosAccount.name
output cosmosDbName string = cosmosDb.name
output cosmosContainerName string = cosmosContainer.name
output acrLoginServer string = acr.properties.loginServer
output webAppUrl string = 'https://${webApp.name}.azurewebsites.net'
output staticEndpoint string = 'https://${staticStorage.name}.z13.web.core.windows.net'
output webIdentityClientId string = webIdentity.properties.clientId
output scraperIdentityClientId string = containerIdentity.properties.clientId

// Log Analytics workspace for Container Apps
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2020-08-01' = {
  name: '${namePrefix}-law'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
  }
}

// Container Apps managed environment
resource containerEnv 'Microsoft.App/managedEnvironments@2022-03-01' = {
  name: '${namePrefix}-env'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: listKeys(logAnalytics.id, logAnalytics.apiVersion).primarySharedKey
      }
    }
  }
  dependsOn: [logAnalytics]
}

// Container App for scraper (image placeholder)
resource scraperApp 'Microsoft.App/containerApps@2022-03-01' = {
  name: '${namePrefix}-scraper'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${containerIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerEnv.id
    configuration: {
      // Registry credentials should be provided (store username/password in Key Vault or GitHub Secrets)
      registries: [
        {
          server: acr.properties.loginServer
          // username/password should be injected via containerApp secrets
          username: ''
        }
      ]
      secrets: [
        {
          name: 'ACR_USERNAME'
          value: ''
        }
        {
          name: 'ACR_PASSWORD'
          value: ''
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'scraper'
          image: '${acr.properties.loginServer}/newsagg/scraper:latest'
          resources: {
            cpu: 0.5
            memory: '1Gi'
          }
          env: [
            {
              name: 'API_BASE_URL'
              value: 'https://${webApp.name}.azurewebsites.net'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 1
      }
    }
  }
  dependsOn: [containerEnv, acr, containerIdentity]
}

  // Role assignment: grant AcrPull on the ACR to the scraper managed identity
  resource acrPullAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
    name: guid(acr.id, containerIdentity.properties.principalId, acrPullRoleDefinitionId)
    scope: acr
    properties: {
      principalId: containerIdentity.properties.principalId
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleDefinitionId)
      principalType: 'ServicePrincipal'
    }
    dependsOn: [acr, containerIdentity]
  }

  // Key Vault role assignments removed; manage Key Vault access outside this template

