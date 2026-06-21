#!/usr/bin/env bash
# Validates HA storage fleet requirements in docker-compose.yml (ha-storage-01).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
COMPOSE_FILE="${REPO_ROOT}/docker-compose.yml"

fail() {
  echo "FAIL: $*" >&2
  exit 1
}

for node in storage-1 storage-2 storage-3; do
  grep -q "^  ${node}:" "${COMPOSE_FILE}" || fail "missing service ${node}"
  grep -q "STORAGE_NODE_ID: \"${node}\"" "${COMPOSE_FILE}" || fail "missing STORAGE_NODE_ID for ${node}"
done

grep -q "opengitbase_storage3_repos:" "${COMPOSE_FILE}" || fail "missing storage-3 volume"
grep -q "storage-3:" "${COMPOSE_FILE}" || fail "missing storage-3 dispatcher dependency"

grep -q 'issue_node_cert storage-3' "${REPO_ROOT}/docker/pki/generate-node-pki.sh" \
  || fail "generate-node-pki.sh must issue storage-3 cert"

grep -q 'create_enrollment storage-3' "${REPO_ROOT}/scripts/bootstrap-fleet.sh" \
  || fail "bootstrap-fleet.sh must enroll storage-3"

echo "OK: three-node storage fleet compose requirements satisfied"
