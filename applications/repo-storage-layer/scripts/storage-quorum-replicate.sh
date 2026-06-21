#!/usr/bin/env bash
# Copyright (c) 2026 OpenGitBase Authors
# SPDX-License-Identifier: LicenseRef-OpenGitBase-1.0

set -euo pipefail

PHYSICAL_PATH="${1:-}"
API_URL="${STORAGE_API_URL:-http://api:8080}"
TOKEN_FILE="${STORAGE_TOKEN_FILE:-/var/lib/opengitbase/api-token}"
NODE_ID="${STORAGE_NODE_ID:-${HOSTNAME:-storage}}"
NODE_CERT_FILE="${STORAGE_NODE_CERT_FILE:-/etc/opengitbase/node.crt}"
WATERMARK_DIR="${STORAGE_WATERMARK_DIR:-/var/lib/opengitbase/watermarks}"

if [ -z "${PHYSICAL_PATH}" ]; then
  echo "storage-quorum-replicate: physical path is required" >&2
  exit 1
fi

if [ ! -f "${TOKEN_FILE}" ]; then
  echo "storage-quorum-replicate: API token unavailable" >&2
  exit 1
fi

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

REPO_ID="$(basename "${PHYSICAL_PATH}" .git)"
mkdir -p "${WATERMARK_DIR}"
WATERMARK_FILE="${WATERMARK_DIR}/${REPO_ID}.txt"
CURRENT="$(cat "${WATERMARK_FILE}" 2>/dev/null || echo 0)"
NEW_WATERMARK=$((CURRENT + 1))
printf '%s' "${NEW_WATERMARK}" > "${WATERMARK_FILE}"

TOKEN="$(cat "${TOKEN_FILE}")"
CERT_THUMBPRINT="$(get_certificate_thumbprint)"
if [ -z "${CERT_THUMBPRINT}" ]; then
  echo "storage-quorum-replicate: certificate thumbprint unavailable" >&2
  exit 1
fi

CONTEXT="$(curl -fsS "${API_URL}/api/v1/storage-nodes/repositories/${REPO_ID}/replication" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "X-Storage-Node-Id: ${NODE_ID}" \
  -H "X-Storage-Node-Certificate-Thumbprint: ${CERT_THUMBPRINT}")"

IS_PRIMARY="$(python3 -c 'import json,sys; print(json.load(sys.stdin).get("isPrimary", False))' <<< "${CONTEXT}")"
if [ "${IS_PRIMARY}" != "True" ]; then
  exit 0
fi

RESPONSE="$(curl -fsS -X POST \
  "${API_URL}/api/v1/storage-nodes/repositories/${REPO_ID}/quorum-replicate" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -H "X-Storage-Node-Id: ${NODE_ID}" \
  -H "X-Storage-Node-Certificate-Thumbprint: ${CERT_THUMBPRINT}" \
  -d "{\"appliedWatermark\": ${NEW_WATERMARK}}")"

SUCCESS="$(python3 -c 'import json,sys; print(json.load(sys.stdin).get("success", False))' <<< "${RESPONSE}")"
if [ "${SUCCESS}" != "True" ]; then
  printf '%s' "${CURRENT}" > "${WATERMARK_FILE}"
  echo "storage-quorum-replicate: quorum replication failed" >&2
  exit 1
fi

exit 0
