#!/usr/bin/env bash
set -euo pipefail

action="${1:?Specify upload, download or delete}"
for variable in VERSION ARTIFACT_NAME ARTIFACT_CONTAINER ARTIFACT_ACCOUNT ARTIFACT_ACCOUNT_KEY; do
  [[ -n "${!variable:-}" ]] || { echo "Missing $variable" >&2; exit 1; }
done
[[ "$VERSION" =~ ^[a-zA-Z0-9][a-zA-Z0-9._-]{0,127}$ ]] || { echo 'Invalid release version' >&2; exit 1; }
[[ "$ARTIFACT_NAME" =~ ^[a-zA-Z0-9][a-zA-Z0-9._-]{0,63}$ ]] || { echo 'Invalid artifact name' >&2; exit 1; }
blob_name="${ARTIFACT_NAME}-${VERSION}.zip"
arguments=(--name "$blob_name" --container-name "$ARTIFACT_CONTAINER" --account-name "$ARTIFACT_ACCOUNT" --account-key "$ARTIFACT_ACCOUNT_KEY" --only-show-errors)
case "$action" in
  upload)
    (cd publish && zip -q -r "../$blob_name" .)
    az storage blob upload "${arguments[@]}" --file "$blob_name" --overwrite false --output none
    ;;
  download)
    mkdir -p publish
    az storage blob download "${arguments[@]}" --file "publish/$blob_name" --output none
    if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
      printf 'package_path=publish/%s\n' "$blob_name" >> "$GITHUB_OUTPUT"
    fi
    ;;
  delete)
    if [[ "$(az storage blob exists "${arguments[@]}" --query exists -o tsv)" == true ]]; then
      az storage blob delete "${arguments[@]}" --output none
    fi
    ;;
  *) echo 'Unknown artifact action' >&2; exit 1 ;;
esac
