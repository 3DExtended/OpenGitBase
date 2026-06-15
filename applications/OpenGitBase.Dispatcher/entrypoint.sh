#!/usr/bin/env bash
# Fetch dispatcher SSH private key from API fleet bootstrap and start sshd.
set -euo pipefail

API_URL="${DISPATCHER_API_URL:-http://api:8080}"
FLEET_BOOTSTRAP_TOKEN="${FLEET_BOOTSTRAP_TOKEN:-}"
PRIVATE_KEY_PATH="${DISPATCHER_STORAGE_SSH_KEY_PATH:-/run/secrets/dispatcher_storage_ssh}"

mkdir -p /var/run/sshd

if [ ! -f /etc/ssh/ssh_host_rsa_key ]; then
  ssh-keygen -A
fi

if [ -n "${FLEET_BOOTSTRAP_TOKEN}" ] && [ ! -f "${PRIVATE_KEY_PATH}" ]; then
  install -d -m 700 "$(dirname "${PRIVATE_KEY_PATH}")"
  curl -fsS "${API_URL}/api/v1/fleet/bootstrap/dispatcher-ssh-private-key" \
    -H "X-Fleet-Bootstrap-Token: ${FLEET_BOOTSTRAP_TOKEN}" \
    | python3 -c 'import json,sys; print(json.load(sys.stdin)["privateKey"])' > "${PRIVATE_KEY_PATH}"
  chmod 600 "${PRIVATE_KEY_PATH}"
fi

exec /usr/sbin/sshd -D -e
