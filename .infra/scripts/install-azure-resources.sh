#!/usr/bin/env bash
set -euo pipefail

for variable in AZURE_NAME_ROOT AZURE_NAME_POSTFIX AZURE_STORAGE_ACCOUNT_NAME; do
  [[ -n "${!variable:-}" ]] || { echo "Missing $variable" >&2; exit 1; }
done
[[ "$AZURE_NAME_ROOT" =~ ^[a-z0-9][a-z0-9-]{1,31}$ ]] || { echo 'Invalid AZURE_NAME_ROOT' >&2; exit 1; }
[[ "$AZURE_NAME_POSTFIX" =~ ^(test|prod)$ ]] || { echo 'AZURE_NAME_POSTFIX must be test or prod' >&2; exit 1; }
for account in "$AZURE_STORAGE_ACCOUNT_NAME"; do
  [[ "$account" =~ ^[a-z0-9]{3,24}$ ]] || { echo 'Invalid storage account name' >&2; exit 1; }
done
memory="${FUNC_INSTANCE_MEMORY:-2048}"
maximum="${FUNC_MAX_INSTANCE_COUNT:-100}"
[[ "$memory" =~ ^(512|2048|4096)$ ]] || { echo 'Invalid FUNC_INSTANCE_MEMORY' >&2; exit 1; }
[[ "$maximum" =~ ^[0-9]+$ ]] && (( maximum >= 40 && maximum <= 1000 )) || { echo 'Invalid FUNC_MAX_INSTANCE_COUNT' >&2; exit 1; }
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
bash "$script_dir/prepare-apim.sh"
for provider in Microsoft.Web Microsoft.Storage Microsoft.Insights Microsoft.OperationalInsights; do
  [[ "$(az provider show -n "$provider" --query registrationState -o tsv --only-show-errors)" == Registered ]] || { echo "Provider must be registered: $provider" >&2; exit 1; }
done
rg="rg-${AZURE_NAME_ROOT}-${AZURE_NAME_POSTFIX}"
if [[ "$(az group exists -n "$rg" -o tsv --only-show-errors)" == false ]]; then
  az group create -n "$rg" -l northeurope --tags "application=$AZURE_NAME_ROOT" "environment=$AZURE_NAME_POSTFIX" -o none --only-show-errors
fi
[[ "$(az group show -n "$rg" --query location -o tsv --only-show-errors)" == northeurope ]] || { echo 'Resource group must be in North Europe' >&2; exit 1; }
outputs="$(az deployment group create -g "$rg" -n "production-calendar-$AZURE_NAME_POSTFIX" --mode Incremental --template-file "$script_dir/../main.bicep" \
  --parameters nameRoot="$AZURE_NAME_ROOT" environmentName="$AZURE_NAME_POSTFIX" storageAccountName="$AZURE_STORAGE_ACCOUNT_NAME" \
  instanceMemoryMB="$memory" maximumInstanceCount="$maximum" \
  --query properties.outputs -o json --only-show-errors)"
function_name="$(jq -er '.functionAppName.value' <<< "$outputs")"
principal_id="$(jq -er '.functionPrincipalId.value' <<< "$outputs")"
if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
  printf 'resource_group=%s\nfunction_name=%s\nfunction_principal_id=%s\n' "$rg" "$function_name" "$principal_id" >> "$GITHUB_OUTPUT"
fi
echo "Infrastructure installed for $function_name"
