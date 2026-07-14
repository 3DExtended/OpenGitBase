#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:8089}"
API_BASE="${API_URL}/api"
OWNER="${OWNER:-admin}"
REPO_SLUG="${REPO_SLUG:-ogb-docs-e2e-$(date +%s)}"
CLI_PROJECT="${CLI_PROJECT:-applications/OpenGitBase.Cli/OpenGitBase.Cli.csproj}"
OUTPUT_DIR="$(mktemp -d)"

require_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Missing required command: $1" >&2
    exit 1
  fi
}

ogb() {
  dotnet run --project "${CLI_PROJECT}" --no-build -- "$@"
}

require_cmd curl
require_cmd dotnet

trap 'rm -rf "${OUTPUT_DIR}" "${OGB_CONFIG_DIR:-}"; if [[ "$(uname)" == "Darwin" ]]; then security delete-generic-password -s "opengitbase-cli/${API_URL}" 2>/dev/null || true; fi' EXIT

echo "==> Checking API health at ${API_URL}"
curl -fsS "${API_URL}/health" >/dev/null

echo "==> Building ogb CLI"
dotnet build "${CLI_PROJECT}" -v q -nologo

echo "==> Signing in as ${OWNER}"
LOGIN_RESPONSE="$(curl -fsS -X POST "${API_BASE}/signin/login" \
  -H "Content-Type: application/json" \
  -d "{\"username\":\"${OWNER}\",\"password\":\"${ADMIN_PASS:-change-me-admin}\"}")"
TOKEN="$(printf '%s' "${LOGIN_RESPONSE}" | tr -d '\n\r\"')"
if [ -z "${TOKEN}" ]; then
  echo "Failed to obtain auth token." >&2
  exit 1
fi

export OGB_CONFIG_DIR="$(mktemp -d)"
export XDG_CONFIG_HOME="${OGB_CONFIG_DIR}"
mkdir -p "${XDG_CONFIG_HOME}/ogb"
cat > "${XDG_CONFIG_HOME}/ogb/hosts.yml" <<EOF
activeHost: ${API_URL}
loggedInUsername: ${OWNER}
EOF
if [[ "$(uname)" == "Darwin" ]]; then
  security add-generic-password -a token -s "opengitbase-cli/${API_URL}" -w "${TOKEN}" -U >/dev/null
else
  cat > "${XDG_CONFIG_HOME}/ogb/credentials.json" <<EOF
{
  "${API_URL}": "${TOKEN}"
}
EOF
fi

echo "==> Creating repository ${OWNER}/${REPO_SLUG}"
curl -fsS -X POST "${API_BASE}/repository/${REPO_SLUG}" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d "{\"repositoryName\":\"${REPO_SLUG}\",\"isPrivate\":false}" >/dev/null

echo "==> Creating PRD and slice discussions"
ogb --hostname "${API_URL}" issue -R "${OWNER}/${REPO_SLUG}" create \
  --title "[PRD] CLI docs smoke" --body "PRD body for smoke test"
ogb --hostname "${API_URL}" issue -R "${OWNER}/${REPO_SLUG}" create \
  --title "[slice] docs-01 — Link and pull" --body "Slice body"

echo "==> ogb issue link"
LINK_OUT="$(ogb --hostname "${API_URL}" issue -R "${OWNER}/${REPO_SLUG}" link 2 --parent 1)"
echo "${LINK_OUT}"
echo "${LINK_OUT}" | grep -qi parent

echo "==> ogb issue links"
LINKS_OUT="$(ogb --hostname "${API_URL}" issue -R "${OWNER}/${REPO_SLUG}" links 2)"
echo "${LINKS_OUT}"
echo "${LINKS_OUT}" | grep -q '#1'

echo "==> ogb docs pull"
PULL_OUT="$(ogb --hostname "${API_URL}" docs -R "${OWNER}/${REPO_SLUG}" pull --output-dir "${OUTPUT_DIR}")"
echo "${PULL_OUT}"
test -f "${OUTPUT_DIR}/docs/prd/cli-docs-smoke.md"
test -f "${OUTPUT_DIR}/docs/issues/docs-01.md"
grep -q 'forge: #1' "${OUTPUT_DIR}/docs/prd/cli-docs-smoke.md"

echo "All ogb docs/link CLI smoke checks passed."
