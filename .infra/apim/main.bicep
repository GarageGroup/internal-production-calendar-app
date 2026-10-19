targetScope = 'resourceGroup'

param apimServiceName string
param functionResourceGroup string
param functionAppName string
@allowed(['test', 'prod'])
param environmentName string

resource apim 'Microsoft.ApiManagement/service@2024-05-01' existing = { name: apimServiceName }
resource functionApp 'Microsoft.Web/sites@2024-04-01' existing = {
  name: functionAppName
  scope: resourceGroup(functionResourceGroup)
}

var apiName = 'production-calendar-${environmentName}'
var keyName = '${apiName}-function-key'
var backendUrl = 'https://${functionApp.properties.defaultHostName}/api/production-calendar'

resource functionKey 'Microsoft.ApiManagement/service/namedValues@2024-05-01' = {
  parent: apim
  name: keyName
  properties: {
    displayName: keyName
    secret: true
    value: listKeys('${functionApp.id}/host/default', '2024-04-01').functionKeys.default
  }
}

resource backend 'Microsoft.ApiManagement/service/backends@2024-05-01' = {
  parent: apim
  name: '${apiName}-backend'
  properties: {
    title: 'Production Calendar (${environmentName})'
    protocol: 'http'
    url: backendUrl
    resourceId: uri(environment().resourceManager, functionApp.id)
  }
}

resource api 'Microsoft.ApiManagement/service/apis@2024-05-01' = {
  parent: apim
  name: apiName
  properties: {
    displayName: 'Production Calendar'
    description: 'Read a calendar day or initialize a complete country calendar.'
    apiType: 'http'
    path: apiName
    protocols: ['https']
    serviceUrl: backendUrl
    subscriptionRequired: true
  }
}

resource product 'Microsoft.ApiManagement/service/products@2024-05-01' = {
  parent: apim
  name: apiName
  properties: {
    displayName: 'Garage Production Calendar'
    description: 'Country production calendar API.'
    state: 'published'
    subscriptionRequired: true
    approvalRequired: true
  }
}

resource productApi 'Microsoft.ApiManagement/service/products/apis@2024-05-01' = {
  parent: product
  name: api.name
}

resource productDevelopers 'Microsoft.ApiManagement/service/products/groups@2024-05-01' = {
  parent: product
  name: 'developers'
}

// Operations and JSON schemas are imported from the deployed release by update-swagger.
resource policy 'Microsoft.ApiManagement/service/apis/policies@2024-05-01' = {
  parent: api
  name: 'policy'
  properties: {
    format: 'rawxml'
    value: replace(replace('''
      <policies>
        <inbound>
          <base />
          <set-backend-service backend-id="__BACKEND_NAME__" />
          <set-header name="x-functions-key" exists-action="override">
            <value>{{__FUNCTION_KEY_NAME__}}</value>
          </set-header>
          <set-query-parameter name="code" exists-action="delete" />
          <set-header name="Ocp-Apim-Subscription-Key" exists-action="delete" />
          <set-query-parameter name="subscription-key" exists-action="delete" />
        </inbound>
        <backend>
          <forward-request timeout="240" />
        </backend>
        <outbound><base /></outbound>
        <on-error><base /></on-error>
      </policies>
      ''', '__BACKEND_NAME__', backend.name), '__FUNCTION_KEY_NAME__', functionKey.properties.displayName)
  }
}

output apimResourceId string = apim.id
output apiPath string = api.properties.path
