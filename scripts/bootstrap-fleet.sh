#!/usr/bin/env bash
# Bootstrap fleet SSH keys and storage node enrollments via admin API.
set -euo pipefail

API_URL="${API_URL:-http://localhost:8080}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PKI_SCRIPT="${SCRIPT_DIR}/../docker/pki/generate-node-pki.sh"

if [ -x "${PKI_SCRIPT}" ]; then
  echo "==> Generating storage node certificates"
  "${PKI_SCRIPT}"
fi

ADMIN_USER="${ADMIN_USER:-admin}"
ADMIN_PASS="${ADMIN_PASS:-change-me-admin}"
ENV_FILE="${ENV_FILE:-docker/.env}"

login() {
  curl -fsS -X POST "${API_URL}/signin/login" \
    -H "Content-Type: application/json" \
    -d "{\"username\":\"${ADMIN_USER}\",\"password\":\"${ADMIN_PASS}\"}"
}

echo "==> Signing in as admin"
TOKEN=$(login | python3 -c 'import json,sys; print(json.load(sys.stdin))')

echo "==> Generating dispatcher SSH keys"
FLEET_JSON=$(curl -fsS -X POST "${API_URL}/admin/fleet/dispatcher-ssh-keys/generate" \
  -H "Authorization: Bearer ${TOKEN}")
FLEET_BOOTSTRAP_TOKEN=$(echo "${FLEET_JSON}" | python3 -c 'import json,sys; print(json.load(sys.stdin)["fleetBootstrapToken"])')

create_enrollment() {
  local node_id="$1"
  curl -fsS -X POST "${API_URL}/admin/storage-enrollments" \
    -H "Authorization: Bearer ${TOKEN}" \
    -H "Content-Type: application/json" \
    -d "{\"nodeId\":\"${node_id}\"}" \
    | python3 -c 'import json,sys; print(json.load(sys.stdin)["enrollmentToken"])'
}

echo "==> Creating storage enrollments"
STORAGE_1_TOKEN=$(create_enrollment storage-1)
STORAGE_2_TOKEN=$(create_enrollment storage-2)

mkdir -p "$(dirname "${ENV_FILE}")"
cat > "${ENV_FILE}" <<EOF
STORAGE_1_ENROLLMENT_TOKEN=${STORAGE_1_TOKEN}
STORAGE_2_ENROLLMENT_TOKEN=${STORAGE_2_TOKEN}
FLEET_BOOTSTRAP_TOKEN=${FLEET_BOOTSTRAP_TOKEN}
EOF

echo "Wrote ${ENV_FILE}"
echo "Start the stack with: docker compose --env-file ${ENV_FILE} up -d --build"
