targetScope = 'resourceGroup'

// Existing APIM placeholder. Add API/backend/operations only with the endpoint contract.
param apimServiceName string
resource apim 'Microsoft.ApiManagement/service@2024-05-01' existing = { name: apimServiceName }
output apimResourceId string = apim.id
