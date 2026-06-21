#!/usr/bin/env bash
# Integration test: PAT-authenticated git push/clone to an organization-owned repository
# via the local Docker Compose HAProxy entry (localhost:8089).
#
# Requires: docker compose stack running (API, storage, dispatchers, ssh-lb).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "${ROOT}"

GIT_HTTP_PORT="${GIT_HTTP_PORT:-8089}"
API_URL="${API_URL:-http://localhost:${GIT_HTTP_PORT}}"
TEST_USER="${TEST_USER:-https-git-org-$(date +%s)}"
TEST_PASS="${TEST_PASS:-Password123!}"
ORG_SLUG="${ORG_SLUG:-org-${TEST_USER}}"
TEST_REPO="${TEST_REPO:-open-git-base}"
WORK_DIR="$(mktemp -d)"

require_cmd() {
  command -v "$1" >/dev/null 2>&1 || {
    echo "error: required command not found: $1" >&2
    exit 1
  }
}

parse_jwt() {
  python3 -c 'import json,sys; raw=sys.stdin.read().strip(); print(json.loads(raw) if raw.startswith("\"") else raw)'
}

cleanup() {
  rm -rf "${WORK_DIR}"
}
trap cleanup EXIT

require_cmd curl
require_cmd git
require_cmd python3

echo "==> Registering test user"
curl -fsS -X POST "${API_URL}/api/register/register" \
  -H "Content-Type: application/json" \
  -d "{\"username\":\"${TEST_USER}\",\"email\":\"${TEST_USER}@example.com\",\"password\":\"${TEST_PASS}\"}" \
  >/dev/null 2>&1 || true

JWT=$(curl -fsS -X POST "${API_URL}/api/signin/login" \
  -H "Content-Type: application/json" \
  -d "{\"username\":\"${TEST_USER}\",\"password\":\"${TEST_PASS}\"}" \
  | parse_jwt)

curl -fsS -X POST "${API_URL}/api/account/debug/verify-email" \
  -H "Authorization: Bearer ${JWT}" >/dev/null 2>&1 || true

echo "==> Creating organization ${ORG_SLUG}"
curl -fsS -X POST "${API_URL}/api/organization" \
  -H "Authorization: Bearer ${JWT}" \
  -H "Content-Type: application/json" \
  -d "{\"modelToCreate\":{\"name\":\"HTTPS Org Test\",\"slug\":\"${ORG_SLUG}\"}}" >/dev/null

echo "==> Creating organization repository ${ORG_SLUG}/${TEST_REPO}"
curl -fsS -X POST "${API_URL}/api/repository/${TEST_REPO}" \
  -H "Authorization: Bearer ${JWT}" \
  -H "Content-Type: application/json" \
  -d "{\"repositoryName\":\"Open Git Base\",\"isPrivate\":false,\"organizationSlug\":\"${ORG_SLUG}\"}" >/dev/null

echo "==> Waiting for storage provisioning"
sleep 10

echo "==> Creating write-scoped PAT"
WRITE_PAT=$(curl -fsS -X POST "${API_URL}/api/git-access-token" \
  -H "Authorization: Bearer ${JWT}" \
  -H "Content-Type: application/json" \
  -d '{"name":"org-write","scope":"write"}' \
  | python3 -c 'import json,sys; print(json.load(sys.stdin)["token"])')

REMOTE="http://git:${WRITE_PAT}@127.0.0.1:${GIT_HTTP_PORT}/${ORG_SLUG}/${TEST_REPO}.git"
REPO_PATH="${ORG_SLUG}/${TEST_REPO}"

echo "==> Verifying access-check allows write for organization owner"
ACCESS=$(curl -fsS -X POST "${API_URL}/api/v1/access-checks/repositories" \
  -H "Content-Type: application/json" \
  -d "{\"accessToken\":\"${WRITE_PAT}\",\"repositoryPath\":\"${REPO_PATH}\",\"operation\":1}")
echo "${ACCESS}" | python3 -c 'import json,sys; payload=json.load(sys.stdin); assert payload["allowed"] is True, payload'

echo "==> Git push via HAProxy HTTP entry"
git -C "${WORK_DIR}" init -b main >/dev/null
git -C "${WORK_DIR}" config user.email "${TEST_USER}@example.com"
git -C "${WORK_DIR}" config user.name "${TEST_USER}"
echo "hello-org-https" > "${WORK_DIR}/README.md"
git -C "${WORK_DIR}" add README.md
git -C "${WORK_DIR}" commit -m "initial" >/dev/null
git -C "${WORK_DIR}" remote add origin "${REMOTE}"
git -C "${WORK_DIR}" push -u origin main

echo "==> Git clone via HAProxy"
CLONE_DIR="${WORK_DIR}/clone"
git clone "${REMOTE}" "${CLONE_DIR}"
grep -q hello-org-https "${CLONE_DIR}/README.md"

echo "==> Creating read-scoped PAT and verifying push is denied"
READ_PAT=$(curl -fsS -X POST "${API_URL}/api/git-access-token" \
  -H "Authorization: Bearer ${JWT}" \
  -H "Content-Type: application/json" \
  -d '{"name":"org-read","scope":"read"}' \
  | python3 -c 'import json,sys; print(json.load(sys.stdin)["token"])')

READ_REMOTE="http://git:${READ_PAT}@127.0.0.1:${GIT_HTTP_PORT}/${ORG_SLUG}/${TEST_REPO}.git"
READ_CODE=$(curl -s -o /dev/null -w "%{http_code}" -u "git:${READ_PAT}" \
  "${API_URL}/${ORG_SLUG}/${TEST_REPO}.git/info/refs?service=git-receive-pack")
if [[ "${READ_CODE}" != "403" ]]; then
  echo "error: read-scoped PAT should have denied push (expected 403, got ${READ_CODE})" >&2
  exit 1
fi

echo "HTTPS git organization repository integration test passed"
