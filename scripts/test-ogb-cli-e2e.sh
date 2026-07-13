#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:8089}"
API_BASE="${API_URL}/api"
OWNER="${OWNER:-admin}"
REPO_SLUG="${REPO_SLUG:-ogb-cli-e2e-$(date +%s)}"
CLI_PROJECT="${CLI_PROJECT:-applications/OpenGitBase.Cli/OpenGitBase.Cli.csproj}"

require_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Missing required command: $1" >&2
    exit 1
  fi
}

json_get() {
  local path="$1"
  python3 -c "import json,sys; print(json.load(sys.stdin)$path)"
}

ogb() {
  dotnet run --project "${CLI_PROJECT}" --no-build -- "$@"
}

require_cmd curl
require_cmd dotnet
require_cmd python3

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
trap 'rm -rf "${OGB_CONFIG_DIR}"; if [[ "$(uname)" == "Darwin" ]]; then security delete-generic-password -s "opengitbase-cli/${API_URL}" 2>/dev/null || true; fi' EXIT
export XDG_CONFIG_HOME="${OGB_CONFIG_DIR}"

echo "==> Storing credentials for ogb"
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

echo "==> ogb issue create"
CREATE_OUT="$(ogb --hostname "${API_URL}" issue -R "${OWNER}/${REPO_SLUG}" create --title "CLI smoke" --body "from test-ogb-cli-e2e.sh")"
echo "${CREATE_OUT}"
echo "${CREATE_OUT}" | grep -q '#1'

echo "==> ogb issue list"
LIST_OUT="$(ogb --hostname "${API_URL}" issue -R "${OWNER}/${REPO_SLUG}" list)"
echo "${LIST_OUT}"
echo "${LIST_OUT}" | grep -q 'CLI smoke'

echo "==> ogb issue comment"
ogb --hostname "${API_URL}" issue -R "${OWNER}/${REPO_SLUG}" comment 1 --body "smoke comment" >/dev/null

echo "==> ogb issue status"
STATUS_OUT="$(ogb --hostname "${API_URL}" issue -R "${OWNER}/${REPO_SLUG}" status 1)"
echo "${STATUS_OUT}"
echo "${STATUS_OUT}" | grep -Eq 'Open|Engaged'

echo "==> ogb issue close"
CLOSE_OUT="$(ogb --hostname "${API_URL}" issue -R "${OWNER}/${REPO_SLUG}" close 1)"
echo "${CLOSE_OUT}"
echo "${CLOSE_OUT}" | grep -q 'Resolved'

echo "==> ogb auth status"
ogb --hostname "${API_URL}" auth status | grep -q "${OWNER}"

echo "All ogb CLI smoke checks passed."
