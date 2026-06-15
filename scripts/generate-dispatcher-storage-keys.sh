#!/usr/bin/env bash
# Generate dispatcher→storage SSH key pair for local docker compose.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SECRETS_DIR="${ROOT}/docker/secrets"
KEY="${SECRETS_DIR}/dispatcher_storage_ssh"

mkdir -p "${SECRETS_DIR}"

if [ ! -f "${KEY}" ]; then
  ssh-keygen -t ed25519 -N "" -f "${KEY}" -C "opengitbase-dispatcher-storage"
  chmod 600 "${KEY}"
  chmod 644 "${KEY}.pub"
  echo "Created ${KEY} and ${KEY}.pub"
else
  echo "Keys already exist at ${KEY}"
fi
