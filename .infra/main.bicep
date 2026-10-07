targetScope = 'resourceGroup'

@minLength(2)
@maxLength(32)
param nameRoot string

@minLength(1)
@maxLength(20)
param environmentName string

@minLength(3)
@maxLength(24)
param storageAccountName string



@allowed([512, 2048, 4096])
param instanceMemoryMB int = 2048

@minValue(40)
@maxValue(1000)
param maximumInstanceCount int = 100

var location = 'northeurope'
var suffix = '${nameRoot}-${environmentName}'
var tags = {
  application: nameRoot
  environment: environmentName
}

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-${suffix}'
  location: location
  tags: tags
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource insights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-${suffix}'
  location: location
  kind: 'web'
  tags: tags
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id
  }
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: { name: 'Standard_LRS' }
  tags: tags
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: true
    defaultToOAuthAuthentication: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource deploymentContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'function-packages'
  properties: { publicAccess: 'None' }
}

resource tableService 'Microsoft.Storage/storageAccounts/tableServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource tables 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = [for tableName in ['ProductionCalendar']: {
  parent: tableService
  name: tableName
}]

resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: 'asp-${suffix}'
  location: location
  kind: 'functionapp'
  tags: tags
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  properties: { reserved: true }
}

resource functionApp 'Microsoft.Web/sites@2024-04-01' = {
  name: 'func-${suffix}'
  location: location
  kind: 'functionapp,linux'
  tags: tags
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        { name: 'AzureWebJobsStorage__accountName', value: storage.name }
        { name: 'AzureWebJobsStorage__credential', value: 'managedidentity' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: insights.properties.ConnectionString }
        { name: 'StorageApi__BaseAddress', value: storage.properties.primaryEndpoints.table }
        { name: 'StorageApi__Timeout', value: '01:00:00' }
        { name: 'ProductionCalendar__Storage__TableName', value: 'ProductionCalendar' }
      ]
    }
    functionAppConfig: {
      runtime: {
        name: 'dotnet-isolated'
        version: '10.0'
      }
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${storage.properties.primaryEndpoints.blob}${deploymentContainer.name}'
          authentication: { type: 'SystemAssignedIdentity' }
        }
      }
      scaleAndConcurrency: {
        instanceMemoryMB: instanceMemoryMB
        maximumInstanceCount: maximumInstanceCount
      }
    }
  }
}

module storageAccess './modules/storage-access.bicep' = {
  name: 'function-storage-access'
  params: {
    storageAccountName: storage.name
    principalId: functionApp.identity.principalId
  }
}

output functionAppName string = functionApp.name
output functionPrincipalId string = functionApp.identity.principalId
output storageAccountName string = storage.name

output tableServiceUri string = storage.properties.primaryEndpoints.table
