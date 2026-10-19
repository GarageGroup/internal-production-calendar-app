#!/usr/bin/env bash
set -euo pipefail

for variable in APIM_RESOURCE_GROUP APIM_SERVICE_NAME AZURE_RESOURCE_GROUP_NAME AZURE_FUNCTION_NAME AZURE_NAME_POSTFIX; do
  [[ -n "${!variable:-}" ]] || { echo "Missing $variable" >&2; exit 1; }
done
[[ "$AZURE_NAME_POSTFIX" =~ ^(test|prod)$ ]] || { echo 'AZURE_NAME_POSTFIX must be test or prod' >&2; exit 1; }

api_name="production-calendar-$AZURE_NAME_POSTFIX"
host_name="$(az functionapp show --resource-group "$AZURE_RESOURCE_GROUP_NAME" --name "$AZURE_FUNCTION_NAME" \
  --query 'properties.defaultHostName || defaultHostName' -o tsv --only-show-errors)"
[[ -n "$host_name" ]] || { echo 'Function hostname is unavailable; check the Function App resource and Azure CLI response.' >&2; exit 1; }
function_key="$(az functionapp keys list --resource-group "$AZURE_RESOURCE_GROUP_NAME" --name "$AZURE_FUNCTION_NAME" \
  --query functionKeys.default -o tsv --only-show-errors)"
[[ -n "$function_key" ]] || { echo 'Function default host key is unavailable; check host readiness and access permissions.' >&2; exit 1; }
if [[ "${GITHUB_ACTIONS:-}" == true ]]; then
  echo "::add-mask::$function_key"
fi

specification="$(mktemp)"
trap 'rm -f "$specification"; unset function_key' EXIT
# Read the specification packaged with the deployed release, not the runner checkout.
curl --silent --show-error --fail --retry 12 --retry-all-errors --retry-delay 10 \
  --connect-timeout 10 --max-time 30 --header "x-functions-key: $function_key" \
  --output "$specification" "https://$host_name/api/swagger/swagger.json"
unset function_key
jq -e '.openapi == "3.0.3" and (.paths | type == "object")' "$specification" > /dev/null

az apim api import --resource-group "$APIM_RESOURCE_GROUP" --service-name "$APIM_SERVICE_NAME" \
  --api-id "$api_name" --path "$api_name" --display-name "Production Calendar ($AZURE_NAME_POSTFIX)" \
  --service-url "https://$host_name/api/production-calendar" --protocols https --subscription-required true \
  --specification-format OpenApiJson --specification-path "$specification" --output none --only-show-errors
echo "Swagger imported into APIM API '$api_name'."
