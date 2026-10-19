#!/usr/bin/env bash
set -euo pipefail

: "${APIM_RESOURCE_GROUP:?Set APIM_RESOURCE_GROUP}"
: "${APIM_SERVICE_NAME:?Set APIM_SERVICE_NAME}"
function_subscription="$(az account show --query id -o tsv --only-show-errors)"
apim_subscription="${APIM_SUBSCRIPTION_ID:-$function_subscription}"
az apim show --subscription "$apim_subscription" --resource-group "$APIM_RESOURCE_GROUP" --name "$APIM_SERVICE_NAME" --output none --only-show-errors

case "${1:-verify}" in
  verify) echo 'Existing APIM verified.'; exit 0 ;;
  deploy) ;;
  *) echo 'Usage: prepare-apim.sh [verify|deploy]' >&2; exit 1 ;;
esac

: "${AZURE_RESOURCE_GROUP_NAME:?Set AZURE_RESOURCE_GROUP_NAME}"
: "${AZURE_FUNCTION_NAME:?Set AZURE_FUNCTION_NAME}"
: "${AZURE_NAME_POSTFIX:?Set AZURE_NAME_POSTFIX}"
[[ "$AZURE_NAME_POSTFIX" =~ ^(test|prod)$ ]] || { echo 'AZURE_NAME_POSTFIX must be test or prod' >&2; exit 1; }

# Host keys may take time to become available after OneDeploy. Never print the key.
ready=false
for attempt in {1..12}; do
  key_length="$(az functionapp keys list --subscription "$function_subscription" --resource-group "$AZURE_RESOURCE_GROUP_NAME" --name "$AZURE_FUNCTION_NAME" \
    --query 'length(functionKeys.default)' -o tsv --only-show-errors 2>/dev/null)" || key_length=0
  if [[ "$key_length" =~ ^[0-9]+$ ]] && (( key_length > 0 )); then
    ready=true
    break
  fi
  echo "Waiting for Function App host key ($attempt/12)..."
  sleep 10
done
[[ "$ready" == true ]] || { echo 'Function App default host key is unavailable; deploy the application first and check its host/storage access.' >&2; exit 1; }

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
az deployment group create --subscription "$apim_subscription" --resource-group "$APIM_RESOURCE_GROUP" \
  --name "production-calendar-apim-$AZURE_NAME_POSTFIX" --mode Incremental \
  --template-file "$script_dir/../apim/main.bicep" \
  --parameters apimServiceName="$APIM_SERVICE_NAME" functionResourceGroup="$AZURE_RESOURCE_GROUP_NAME" \
    functionAppName="$AZURE_FUNCTION_NAME" functionSubscriptionId="$function_subscription" environmentName="$AZURE_NAME_POSTFIX" \
  --output none --only-show-errors
echo "Configured APIM API production-calendar-$AZURE_NAME_POSTFIX."
