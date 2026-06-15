#!/usr/bin/env bash
# Copyright (c) 2026 OpenGitBase Authors
# SPDX-License-Identifier: LicenseRef-OpenGitBase-1.0

set -euo pipefail

API_URL="${STORAGE_API_URL:-http://api:8080}"
NODE_ID="${STORAGE_NODE_ID:-${HOSTNAME:-storage}}"
INTERNAL_HOST="${STORAGE_INTERNAL_HOST:-${HOSTNAME:-storage}}"
INTERNAL_SSH_PORT="${STORAGE_INTERNAL_SSH_PORT:-22}"
INTERNAL_HTTP_PORT="${STORAGE_INTERNAL_HTTP_PORT:-8081}"
TOKEN_FILE="${STORAGE_TOKEN_FILE:-/var/lib/opengitbase/api-token}"
HEARTBEAT_INTERVAL="${STORAGE_HEARTBEAT_INTERVAL:-30}"
ENROLLMENT_TOKEN="${STORAGE_ENROLLMENT_TOKEN:-}"
NODE_CERT_FILE="${STORAGE_NODE_CERT_FILE:-/etc/opengitbase/node.crt}"

get_certificate_thumbprint() {
  if [ ! -f "${NODE_CERT_FILE}" ]; then
    echo ""
    return
  fi
  openssl x509 -in "${NODE_CERT_FILE}" -noout -fingerprint -sha256 \
    | cut -d= -f2 \
    | tr -d ':' \
    | tr '[:lower:]' '[:upper:]'
}

get_disk_stats() {
  local mount="/srv/git"
  if ! df -B1 --output=avail,size "$mount" >/dev/null 2>&1; then
    echo "0 0"
    return
  fi
  df -B1 --output=avail,size "$mount" | tail -n1
}

register_node() {
  local free total
  read -r free total < <(get_disk_stats)
  local payload
  payload=$(cat <<EOF
{
  "nodeId": "${NODE_ID}",
  "internalHost": "${INTERNAL_HOST}",
  "internalSshPort": ${INTERNAL_SSH_PORT},
  "internalHttpPort": ${INTERNAL_HTTP_PORT},
  "freeBytesAvailable": ${free:-0},
  "totalBytesAvailable": ${total:-0}
}
EOF
)

  local response=""
  local enrollment_header=()
  local cert_thumbprint
  cert_thumbprint="$(get_certificate_thumbprint)"
  if [ -z "${cert_thumbprint}" ]; then
    echo "storage-agent: node certificate thumbprint unavailable (${NODE_CERT_FILE})" >&2
    return 1
  fi
  if [ -n "${ENROLLMENT_TOKEN}" ]; then
    enrollment_header=(-H "X-Storage-Enrollment-Token: ${ENROLLMENT_TOKEN}")
  fi
  for _ in $(seq 1 30); do
    if response=$(curl -fsS -X POST "${API_URL}/api/v1/storage-nodes/register" \
      -H "Content-Type: application/json" \
      -H "X-Storage-Node-Certificate-Thumbprint: ${cert_thumbprint}" \
      "${enrollment_header[@]}" \
      -d "${payload}" 2>/dev/null); then
      break
    fi
    sleep 2
  done

  if [ -z "${response}" ]; then
    echo "storage-agent: failed to register with API at ${API_URL}" >&2
    return 1
  fi

  local token interval
  token=$(echo "${response}" | python3 -c 'import json,sys; print(json.load(sys.stdin).get("apiToken",""))')
  interval=$(echo "${response}" | python3 -c 'import json,sys; print(json.load(sys.stdin).get("heartbeatIntervalSeconds",30))')

  if [ -n "${token}" ]; then
    install -d -m 700 "$(dirname "${TOKEN_FILE}")"
    printf '%s' "${token}" > "${TOKEN_FILE}"
    chmod 600 "${TOKEN_FILE}"
    export STORAGE_API_TOKEN="${token}"
  elif [ -f "${TOKEN_FILE}" ]; then
    STORAGE_API_TOKEN="$(cat "${TOKEN_FILE}")"
    export STORAGE_API_TOKEN
  fi

  HEARTBEAT_INTERVAL="${interval:-${HEARTBEAT_INTERVAL}}"
}

send_heartbeat() {
  local token
  if [ ! -f "${TOKEN_FILE}" ]; then
    return 1
  fi
  token=$(cat "${TOKEN_FILE}")
  local cert_thumbprint
  cert_thumbprint="$(get_certificate_thumbprint)"
  if [ -z "${cert_thumbprint}" ]; then
    return 1
  fi
  local free total
  read -r free total < <(get_disk_stats)
  local payload
  payload=$(cat <<EOF
{
  "nodeId": "${NODE_ID}",
  "freeBytesAvailable": ${free:-0},
  "totalBytesAvailable": ${total:-0}
}
EOF
)
  curl -fsS -X POST "${API_URL}/api/v1/storage-nodes/heartbeat" \
    -H "Content-Type: application/json" \
    -H "Authorization: Bearer ${token}" \
    -H "X-Storage-Node-Certificate-Thumbprint: ${cert_thumbprint}" \
    -d "${payload}" >/dev/null
}

heartbeat_loop() {
  while true; do
    send_heartbeat || register_node || true
    sleep "${HEARTBEAT_INTERVAL}"
  done
}

configure_dispatcher_authorized_keys() {
  local pubkey="${DISPATCHER_SSH_PUBLIC_KEY:-}"
  if [ -z "${pubkey}" ] && [ -n "${DISPATCHER_SSH_PUBLIC_KEY_FILE:-}" ] && [ -f "${DISPATCHER_SSH_PUBLIC_KEY_FILE}" ]; then
    pubkey="$(cat "${DISPATCHER_SSH_PUBLIC_KEY_FILE}")"
  fi
  if [ -z "${pubkey}" ] && [ -n "${ENROLLMENT_TOKEN}" ]; then
    pubkey=$(curl -fsS "${API_URL}/api/v1/storage-nodes/bootstrap/dispatcher-ssh-public-key" \
      -H "X-Storage-Enrollment-Token: ${ENROLLMENT_TOKEN}" \
      -H "X-Storage-Node-Id: ${NODE_ID}" \
      | python3 -c 'import json,sys; print(json.load(sys.stdin).get("publicKey",""))')
  fi
  if [ -z "${pubkey}" ]; then
    echo "entrypoint: dispatcher SSH public key is not configured" >&2
    exit 1
  fi
  printf '%s\n' "${pubkey}" > /home/git/.ssh/authorized_keys
  chmod 600 /home/git/.ssh/authorized_keys
  chown git:git /home/git/.ssh/authorized_keys
}

configure_dispatcher_authorized_keys
register_node

export STORAGE_API_TOKEN
if [ -z "${STORAGE_API_TOKEN:-}" ] && [ -f "${TOKEN_FILE}" ]; then
  STORAGE_API_TOKEN="$(cat "${TOKEN_FILE}")"
  export STORAGE_API_TOKEN
fi

if [ -z "${STORAGE_API_TOKEN:-}" ]; then
  echo "entrypoint: storage API token unavailable after registration" >&2
  exit 1
fi

heartbeat_loop &
