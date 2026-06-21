#!/usr/bin/env bash
# Increment .deploy-patch on the host when production deploy builds are enabled.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PATCH_FILE="${REPO_ROOT}/.deploy-patch"

if [ "${DEPLOY_BUMP:-0}" != "1" ]; then
  exit 0
fi

if [ -d "${PATCH_FILE}" ]; then
  rm -rf "${PATCH_FILE}"
fi

patch=0
if [ -f "${PATCH_FILE}" ]; then
  patch="$(tr -d ' \n\r\t' < "${PATCH_FILE}")"
fi

patch=$((patch + 1))
printf '%s' "${patch}" > "${PATCH_FILE}"
