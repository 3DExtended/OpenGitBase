#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:8089}"
API_BASE="${API_URL}/api"
OWNER="${OWNER:-admin}"
REPO_SLUG="${REPO_SLUG:-ogb-mr-e2e-$(date +%s)}"
CLI_PROJECT="${CLI_PROJECT:-applications/OpenGitBase.Cli/OpenGitBase.Cli.csproj}"
FEATURE_BRANCH="${FEATURE_BRANCH:-feature/cli-mr-smoke}"

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
require_cmd python3
require_cmd git

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
trap 'rm -rf "${OGB_CONFIG_DIR}" "${WORK_DIR:-}"; if [[ "$(uname)" == "Darwin" ]]; then security delete-generic-password -s "opengitbase-cli/${API_URL}" 2>/dev/null || true; fi' EXIT
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

echo "==> Creating PAT for git push"
PAT_RESPONSE="$(curl -fsS -X POST "${API_BASE}/repository/by-slug/${OWNER}/${REPO_SLUG}/personal-access-tokens" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d "{\"name\":\"mr-smoke-pat\",\"scopes\":[\"write_repository\"]}")"
PAT="$(printf '%s' "${PAT_RESPONSE}" | python3 -c "import json,sys; print(json.load(sys.stdin)['token'])")"

WORK_DIR="$(mktemp -d)"
git -C "${WORK_DIR}" init -b main
git -C "${WORK_DIR}" config user.email "${OWNER}@example.com"
git -C "${WORK_DIR}" config user.name "${OWNER}"
echo "initial" > "${WORK_DIR}/README.md"
git -C "${WORK_DIR}" add README.md
git -C "${WORK_DIR}" commit -m "initial commit"
git -C "${WORK_DIR}" remote add origin "https://x-access-token:${PAT}@localhost:8443/${OWNER}/${REPO_SLUG}.git"
git -C "${WORK_DIR}" push -u origin main

git -C "${WORK_DIR}" checkout -b "${FEATURE_BRANCH}"
echo "feature change" >> "${WORK_DIR}/README.md"
git -C "${WORK_DIR}" add README.md
git -C "${WORK_DIR}" commit -m "feature commit"
git -C "${WORK_DIR}" push -u origin "${FEATURE_BRANCH}"

echo "==> ogb mr create"
CREATE_OUT="$(ogb --hostname "${API_URL}" mr -R "${OWNER}/${REPO_SLUG}" create --title "CLI MR smoke" --head "${FEATURE_BRANCH}" --base main)"
echo "${CREATE_OUT}"
echo "${CREATE_OUT}" | grep -q '#1'

echo "==> ogb mr list"
LIST_OUT="$(ogb --hostname "${API_URL}" mr -R "${OWNER}/${REPO_SLUG}" list)"
echo "${LIST_OUT}"
echo "${LIST_OUT}" | grep -q 'CLI MR smoke'

echo "==> ogb mr view"
VIEW_OUT="$(ogb --hostname "${API_URL}" mr -R "${OWNER}/${REPO_SLUG}" view 1)"
echo "${VIEW_OUT}"
echo "${VIEW_OUT}" | grep -q "${FEATURE_BRANCH}"

echo "==> ogb mr close"
CLOSE_OUT="$(ogb --hostname "${API_URL}" mr -R "${OWNER}/${REPO_SLUG}" close 1)"
echo "${CLOSE_OUT}"
echo "${CLOSE_OUT}" | grep -q 'Closed'

echo "All ogb mr CLI smoke checks passed."
