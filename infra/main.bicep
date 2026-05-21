targetScope = 'resourceGroup'

@description('Azure region for all resources.')
// Default to New Zealand North for deployments unless overridden.
param location string = 'newzealandnorth'

@description('Naming prefix used for generated resources.')
param prefix string = 'newsagg'

@secure()
@description('Administrator password for the PostgreSQL server.')
param postgresAdminPassword string

@description('PostgreSQL administrator username.')
param postgresAdminUsername string = 'pgadmin'

@description('Database name for the application.')
param postgresDbName string = 'newsagg'

@description('App Service plan SKU name.')
param appServicePlanSku string = 'B1'

//var suffix = uniqueString(resourceGroup().id)
var sanitizedPrefix = toLower(replace(prefix, '-', ''))

//var webAppName = toLower('${prefix}-web')
var webAppName = toLower('${prefix}-web')
var functionAppName = toLower('${prefix}-func')
var frontendAppName = toLower('${prefix}-ui')
var acrName = toLower('${sanitizedPrefix}acr')
var postgresServerName = toLower('${sanitizedPrefix}pg')
var keyVaultName = toLower('${sanitizedPrefix}kv')
var storageAccountName = toLower('${sanitizedPrefix}sa')
var postgresConnectionString = 'Host=${postgresServerName}.postgres.database.azure.com;Port=5432;Database=${postgresDbName};Username=${postgresAdminUsername};Password=${postgresAdminPassword};Ssl Mode=Require;Timeout=30'

resource storage 'Microsoft.Storage/storageAccounts@2026-04-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: true
    publicNetworkAccess: 'Enabled'
  }
}

resource appServicePlan 'Microsoft.Web/serverfarms@2025-03-01' = {
  name: '${prefix}-plan'
  location: location
  kind: 'linux'
  sku: {
    name: appServicePlanSku
    tier: 'Basic'
    capacity: 1
  }
  properties: {
    reserved: true
  }
}

resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: postgresServerName
  location: location
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    administratorLogin: postgresAdminUsername
    administratorLoginPassword: postgresAdminPassword
    version: '17'
    storage: {
      storageSizeGB: 32
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
    network: {
      publicNetworkAccess: 'Enabled'
    }
    authentication: {
      passwordAuth: 'Enabled'
      activeDirectoryAuth: 'Disabled'
    }
  }
}

resource postgresFirewall 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = {
  name: '${postgres.name}/AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource postgresDb 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  name: '${postgres.name}/${postgresDbName}'
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

resource webApp 'Microsoft.Web/sites@2025-03-01' = {
  name: webAppName
  location: location
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: true
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'ASPNETCORE_URLS'
          value: 'http://+:8080'
        }
        {
          name: 'Scraper__PythonScriptPath'
          value: 'scraper/main.py'
        }
        {
          name: 'ConnectionStrings__NewsAggregator'
          value: '@Microsoft.KeyVault(SecretUri=https://${keyVaultName}.vault.azure.net/secrets/PostgresConnectionString/)'
        }
      ]
    }
  }
}

resource frontendApp 'Microsoft.Web/sites@2025-03-01' = {
  name: frontendAppName
  location: location
  kind: 'app,linux'
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOCKER|${acr.properties.loginServer}/newsagg-frontend:latest'
      alwaysOn: true
      appSettings: [
        {
          name: 'WEBSITES_PORT'
          value: '80'
        }
        {
          name: 'BACKEND_URL'
          value: 'https://${webAppName}.azurewebsites.net'
        }
        {
          name: 'DOCKER_REGISTRY_SERVER_URL'
          value: 'https://${acr.properties.loginServer}'
        }
        {
          name: 'DOCKER_REGISTRY_SERVER_USERNAME'
          value: acr.listCredentials().username
        }
        {
          name: 'DOCKER_REGISTRY_SERVER_PASSWORD'
          value: acr.listCredentials().passwords[0].value
        }
        {
          name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE'
          value: 'false'
        }
      ]
    }
  }
}

resource functionApp 'Microsoft.Web/sites@2025-03-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      // Bump to latest supported Python runtime for Functions
      linuxFxVersion: 'PYTHON|3.13'
      alwaysOn: true
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${listKeys(storage.id, storage.apiVersion).keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'python'
        }
        {
          name: 'AzureWebJobsFeatureFlags'
          value: 'EnableWorkerIndexing'
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
        {
          name: 'SCRAPER_REFRESH_URL'
          value: 'https://${webAppName}.azurewebsites.net/api/scraper/refresh'
        }
        {
          name: 'SCRAPE_SCHEDULE_CRON'
          value: '0 */30 * * * *'
        }
      ]
    }
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2025-05-01' = {
  name: keyVaultName
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enabledForTemplateDeployment: true
    enableSoftDelete: true
    enablePurgeProtection: true
    publicNetworkAccess: 'Enabled'
  }
}

resource webAppKeyVaultRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, webApp.identity.principalId, '4633458b-17de-408a-b874-0445c86b69e6')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalId: webApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource postgresConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2025-05-01' = {
  parent: keyVault
  name: 'PostgresConnectionString'
  properties: {
    value: postgresConnectionString
  }
}

output webAppUrl string = 'https://${webApp.name}.azurewebsites.net'
output frontendAppUrl string = 'https://${frontendApp.name}.azurewebsites.net'
output functionAppUrl string = 'https://${functionApp.name}.azurewebsites.net'
output acrLoginServer string = acr.properties.loginServer
output keyVaultUri string = keyVault.properties.vaultUri
output postgresServerFqdn string = postgres.properties.fullyQualifiedDomainName
