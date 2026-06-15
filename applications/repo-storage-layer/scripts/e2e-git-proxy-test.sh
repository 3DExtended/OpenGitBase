#!/usr/bin/env bash
# End-to-end integration test: API create repo → git push/clone via dispatcher.
# Requires: docker compose stack running, dispatcher keys generated.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "${ROOT}"

DISPATCHER_PORT="${DISPATCHER_PORT:-2223}"
API_URL="${API_URL:-http://localhost:8080}"
TEST_USER="${TEST_USER:-gitproxy-e2e}"
TEST_PASS="${TEST_PASS:-Password123!}"
TEST_REPO="${TEST_REPO:-e2e-repo}"
WORK_DIR="$(mktemp -d)"
SSH_KEY="${WORK_DIR}/id_ed25519"

cleanup() {
  rm -rf "${WORK_DIR}"
}
trap cleanup EXIT

echo "==> Generating dispatcher storage keys if missing"
bash scripts/generate-dispatcher-storage-keys.sh

echo "==> Registering test user and creating repository via API"
# Sign up (ignore conflict)
curl -fsS -X POST "${API_URL}/authorization/register" \
  -H "Content-Type: application/json" \
  -d "{\"username\":\"${TEST_USER}\",\"email\":\"${TEST_USER}@example.com\",\"password\":\"${TEST_PASS}\"}" \
  >/dev/null 2>&1 || true

TOKEN=$(curl -fsS -X POST "${API_URL}/authorization/sign-in" \
  -H "Content-Type: application/json" \
  -d "{\"username\":\"${TEST_USER}\",\"password\":\"${TEST_PASS}\"}" \
  | python3 -c 'import json,sys; print(json.load(sys.stdin)["token"])')

curl -fsS -X POST "${API_URL}/repository/${TEST_REPO}" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"repositoryName":"E2E Repo","isPrivate":false}' >/dev/null

echo "==> Waiting for healthy storage nodes"
for _ in $(seq 1 30); do
  COUNT=$(curl -fsS "${API_URL}/api/v1/storage-nodes/healthy" | python3 -c 'import json,sys; print(len(json.load(sys.stdin)))')
  if [ "${COUNT}" -ge 1 ]; then
    break
  fi
  sleep 2
done

echo "==> Generating SSH key and registering with API"
ssh-keygen -t ed25519 -N "" -f "${SSH_KEY}" -C "${TEST_USER}@e2e" >/dev/null
PUBKEY=$(cat "${SSH_KEY}.pub")
curl -fsS -X POST "${API_URL}/public-git-ssh-key" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d "{\"title\":\"e2e\",\"publicKey\":\"${PUBKEY}\"}" >/dev/null

echo "==> Git push via dispatcher"
export GIT_SSH_COMMAND="ssh -i ${SSH_KEY} -p ${DISPATCHER_PORT} -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null"
git -C "${WORK_DIR}" init -b main
git -C "${WORK_DIR}" config user.email "${TEST_USER}@example.com"
git -C "${WORK_DIR}" config user.name "${TEST_USER}"
echo "hello" > "${WORK_DIR}/README.md"
git -C "${WORK_DIR}" add README.md
git -C "${WORK_DIR}" commit -m "initial"
git -C "${WORK_DIR}" remote add origin "ssh://git@localhost:${DISPATCHER_PORT}/${TEST_USER}/${TEST_REPO}"
git -C "${WORK_DIR}" push -u origin main

echo "==> Git clone via dispatcher"
CLONE_DIR="${WORK_DIR}/clone"
git clone "ssh://git@localhost:${DISPATCHER_PORT}/${TEST_USER}/${TEST_REPO}" "${CLONE_DIR}"
grep -q hello "${CLONE_DIR}/README.md"

echo "E2E git storage proxy test passed."
