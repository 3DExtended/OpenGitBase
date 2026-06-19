#!/usr/bin/env bash
# End-to-end integration test: PAT-authenticated git push/clone over HTTPS via HAProxy.
# Requires: docker compose stack running (API, web, storage, dispatchers, ssh-lb).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "${ROOT}"

GIT_HTTP_PORT="${GIT_HTTP_PORT:-8089}"
API_URL="${API_URL:-http://localhost:${GIT_HTTP_PORT}}"
TEST_USER="${TEST_USER:-https-git-e2e-$(date +%s)}"
TEST_PASS="${TEST_PASS:-Password123!}"
TEST_REPO="${TEST_REPO:-https-e2e-repo}"
WORK_DIR="$(mktemp -d)"

cleanup() {
  rm -rf "${WORK_DIR}"
}
trap cleanup EXIT

echo "==> Registering test user and creating repository via API"
curl -fsS -X POST "${API_URL}/api/register/register" \
  -H "Content-Type: application/json" \
  -d "{\"username\":\"${TEST_USER}\",\"email\":\"${TEST_USER}@example.com\",\"password\":\"${TEST_PASS}\"}" \
  >/dev/null 2>&1 || true

JWT=$(curl -fsS -X POST "${API_URL}/api/signin/login" \
  -H "Content-Type: application/json" \
  -d "{\"username\":\"${TEST_USER}\",\"password\":\"${TEST_PASS}\"}" \
  | python3 -c 'import json,sys; raw=sys.stdin.read().strip(); print(json.loads(raw) if raw.startswith("\"") or raw.startswith("{") else raw)')

curl -fsS -X POST "${API_URL}/api/account/debug/verify-email" \
  -H "Authorization: Bearer ${JWT}" >/dev/null 2>&1 || true

curl -fsS -X POST "${API_URL}/api/repository/${TEST_REPO}" \
  -H "Authorization: Bearer ${JWT}" \
  -H "Content-Type: application/json" \
  -d '{"repositoryName":"HTTPS E2E Repo","isPrivate":false}' >/dev/null

echo "==> Waiting for storage provisioning"
sleep 10

echo "==> Creating write-scoped PAT"
WRITE_PAT=$(curl -fsS -X POST "${API_URL}/api/git-access-token" \
  -H "Authorization: Bearer ${JWT}" \
  -H "Content-Type: application/json" \
  -d '{"name":"e2e-write","scope":"write"}' \
  | python3 -c 'import json,sys; print(json.load(sys.stdin)["token"])')

REMOTE="http://git:${WRITE_PAT}@127.0.0.1:${GIT_HTTP_PORT}/${TEST_USER}/${TEST_REPO}.git"

echo "==> Git push via HAProxy HTTP entry"
git -C "${WORK_DIR}" init -b main >/dev/null
git -C "${WORK_DIR}" config user.email "${TEST_USER}@example.com"
git -C "${WORK_DIR}" config user.name "${TEST_USER}"
echo "hello-https" > "${WORK_DIR}/README.md"
git -C "${WORK_DIR}" add README.md
git -C "${WORK_DIR}" commit -m "initial" >/dev/null
git -C "${WORK_DIR}" remote add origin "${REMOTE}"
git -C "${WORK_DIR}" push -u origin main

echo "==> Git clone via HAProxy"
CLONE_DIR="${WORK_DIR}/clone"
git clone "${REMOTE}" "${CLONE_DIR}"
grep -q hello-https "${CLONE_DIR}/README.md"

echo "==> Creating read-scoped PAT and verifying push is denied"
READ_PAT=$(curl -fsS -X POST "${API_URL}/api/git-access-token" \
  -H "Authorization: Bearer ${JWT}" \
  -H "Content-Type: application/json" \
  -d '{"name":"e2e-read","scope":"read"}' \
  | python3 -c 'import json,sys; print(json.load(sys.stdin)["token"])')

READ_WORK="${WORK_DIR}/read-test"
git clone "${REMOTE}" "${READ_WORK}" >/dev/null
echo "blocked" >> "${READ_WORK}/README.md"
git -C "${READ_WORK}" add README.md
git -C "${READ_WORK}" commit -m "should fail" >/dev/null
READ_REMOTE="http://git:${READ_PAT}@127.0.0.1:${GIT_HTTP_PORT}/${TEST_USER}/${TEST_REPO}.git"
git -C "${READ_WORK}" remote set-url origin "${READ_REMOTE}"
if git -C "${READ_WORK}" push origin main 2>/dev/null; then
  echo "error: read-scoped PAT should have denied push" >&2
  exit 1
fi

echo "HTTPS git E2E test passed"
