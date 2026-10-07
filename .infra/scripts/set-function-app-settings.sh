#!/usr/bin/env bash
set -euo pipefail

for variable in AZURE_RESOURCE_GROUP_NAME AZURE_FUNCTION_NAME AZURE_STORAGE_ACCOUNT_NAME; do
  [[ -n "${!variable:-}" ]] || { echo "Missing $variable" >&2; exit 1; }
done
[[ "$AZURE_STORAGE_ACCOUNT_NAME" =~ ^[a-z0-9]{3,24}$ ]] || { echo 'Invalid storage account name' >&2; exit 1; }
table_uri="$(az storage account show --name "$AZURE_STORAGE_ACCOUNT_NAME" --resource-group "$AZURE_RESOURCE_GROUP_NAME" --query primaryEndpoints.table -o tsv --only-show-errors)"
[[ "$table_uri" == https://*/ ]] || { echo 'Table endpoint must be HTTPS with a trailing slash' >&2; exit 1; }
settings=(
  "Info__DeployDateTime=$(date -u +'%Y-%m-%dT%H:%M:%SZ')"
  "StorageApi__BaseAddress=$table_uri"
  "StorageApi__Timeout=01:00:00"
  "ProductionCalendar__Storage__TableName=ProductionCalendar"
  "AzureWebJobsStorage__accountName=$AZURE_STORAGE_ACCOUNT_NAME"
  'AzureWebJobsStorage__credential=managedidentity'
)
az functionapp config appsettings set --resource-group "$AZURE_RESOURCE_GROUP_NAME" --name "$AZURE_FUNCTION_NAME" \
  --settings "${settings[@]}" --output none --only-show-errors
echo "Updated Function App settings for $AZURE_FUNCTION_NAME"
