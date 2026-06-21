#!/bin/sh
# Emits shell exports for Nuxt deploy version build args.
set -eu

VERSION_FILE="${VERSION_FILE:-/repo/VERSION}"
PATCH_FILE="${PATCH_FILE:-/repo/.deploy-patch}"
GIT_SHA="${GIT_SHA:-unknown}"

major_minor="$(tr -d ' \n\r\t' < "${VERSION_FILE}")"

if [ -f "${PATCH_FILE}" ] && [ ! -d "${PATCH_FILE}" ]; then
  patch="$(tr -d ' \n\r\t' < "${PATCH_FILE}")"
else
  patch=0
fi

deploy_version="v${major_minor}.${patch}"

printf 'export NUXT_PUBLIC_DEPLOY_VERSION=%s\n' "${deploy_version}"
printf 'export NUXT_PUBLIC_DEPLOY_SHA=%s\n' "${GIT_SHA}"
