#!/usr/bin/env bash
#
# Smoke test: git clone/push via dispatcher Smart HTTP with a PAT.
# Requires docker compose stack running and dispatcher HTTP published on DISPATCHER_HTTP_PORT.
#
# Usage:
#   DISPATCHER_HTTP_PORT=8822 applications/OpenGitBase.Dispatcher/scripts/git-http-smoke-test.sh

set -euo pipefail

DISPATCHER_HTTP_PORT="${DISPATCHER_HTTP_PORT:-8822}"
API_URL="${API_URL:-http://localhost:8080}"
TEST_USER="${TEST_USER:-git-http-smoke}"
TEST_PASS="${TEST_PASS:-Password123!}"
TEST_REPO="${TEST_REPO:-git-http-smoke-repo}"
WORK_DIR="$(mktemp -d)"

cleanup() {
  rm -rf "${WORK_DIR}"
}
trap cleanup EXIT

require_cmd() {
  command -v "$1" >/dev/null 2>&1 || {
    echo "error: required command not found: $1" >&2
    exit 1
  }
}

require_cmd curl
require_cmd git
require_cmd python3

echo "==> Registering test user"
curl -fsS -X POST "${API_URL}/authorization/register" \
  -H "Content-Type: application/json" \
  -d "{\"username\":\"${TEST_USER}\",\"email\":\"${TEST_USER}@example.com\",\"password\":\"${TEST_PASS}\"}" \
  >/dev/null 2>&1 || true

JWT=$(curl -fsS -X POST "${API_URL}/authorization/sign-in" \
  -H "Content-Type: application/json" \
  -d "{\"username\":\"${TEST_USER}\",\"password\":\"${TEST_PASS}\"}" \
  | python3 -c 'import json,sys; print(json.load(sys.stdin)["token"])')

echo "==> Creating repository"
curl -fsS -X POST "${API_URL}/repository/${TEST_REPO}" \
  -H "Authorization: Bearer ${JWT}" \
  -H "Content-Type: application/json" \
  -d '{"repositoryName":"Git HTTP Smoke","isPrivate":false}' >/dev/null

echo "==> Creating read/write PAT"
PAT=$(curl -fsS -X POST "${API_URL}/api/v1/git/access-tokens" \
  -H "Authorization: Bearer ${JWT}" \
  -H "Content-Type: application/json" \
  -d '{"name":"smoke","scope":"write"}' \
  | python3 -c 'import json,sys; print(json.load(sys.stdin)["token"])')

REMOTE="http://127.0.0.1:${DISPATCHER_HTTP_PORT}/${TEST_USER}/${TEST_REPO}.git"
AUTH_HEADER="Authorization: Basic $(printf 'git:%s' "${PAT}" | base64 | tr -d '\n')"

echo "==> Probing info/refs via dispatcher"
curl -fsS -H "${AUTH_HEADER}" \
  "${REMOTE}/info/refs?service=git-upload-pack" >/dev/null

echo "==> Git push via dispatcher HTTP"
git -C "${WORK_DIR}" init -b main >/dev/null
git -C "${WORK_DIR}" config user.email "${TEST_USER}@example.com"
git -C "${WORK_DIR}" config user.name "${TEST_USER}"
echo "hello" > "${WORK_DIR}/README.md"
git -C "${WORK_DIR}" add README.md
git -C "${WORK_DIR}" commit -m "initial" >/dev/null
git -C "${WORK_DIR}" remote add origin "http://git:${PAT}@127.0.0.1:${DISPATCHER_HTTP_PORT}/${TEST_USER}/${TEST_REPO}.git"
git -C "${WORK_DIR}" push -u origin main

echo "==> Git clone via dispatcher HTTP"
git clone "http://git:${PAT}@127.0.0.1:${DISPATCHER_HTTP_PORT}/${TEST_USER}/${TEST_REPO}.git" "${WORK_DIR}/clone"
grep -q hello "${WORK_DIR}/clone/README.md"

echo "dispatcher git-http smoke test passed"
