#!/usr/bin/env bash
set -euo pipefail

: "${APIM_RESOURCE_GROUP:?Set APIM_RESOURCE_GROUP}"
: "${APIM_SERVICE_NAME:?Set APIM_SERVICE_NAME}"
az apim show --resource-group "$APIM_RESOURCE_GROUP" --name "$APIM_SERVICE_NAME" --output none --only-show-errors
echo 'Existing APIM verified; API, backend, policies and operations are deferred.'
