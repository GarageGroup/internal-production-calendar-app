param storageAccountName string
param principalId string

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' existing = { name: storageAccountName }

resource assignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for roleId in [
  'b7e6dc6d-f1e8-4753-8033-0f276bb0955b' // Storage Blob Data Owner: Functions host/deployment storage
  '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3' // Storage Table Data Contributor
]: {
  scope: storage
  name: guid(storage.id, principalId, roleId)
  properties: {
    principalId: principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleId)
  }
}]
