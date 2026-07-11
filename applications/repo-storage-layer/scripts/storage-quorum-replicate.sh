#!/usr/bin/env bash
# Copyright (c) 2026 OpenGitBase Authors
# SPDX-License-Identifier: LicenseRef-OpenGitBase-1.0

set -euo pipefail

PHYSICAL_PATH="${1:-}"
PUSH_REF="${2:-}"
AFTER_SHA="${3:-}"
CONFIG_DIR="${STORAGE_CONFIG_DIR:-/var/lib/opengitbase}"
API_URL="${STORAGE_API_URL:-$(cat "${CONFIG_DIR}/api-url" 2>/dev/null || echo http://api-lb:8080)}"
TOKEN_FILE="${STORAGE_TOKEN_FILE:-${CONFIG_DIR}/api-token}"
NODE_ID="${STORAGE_NODE_ID:-$(cat "${CONFIG_DIR}/node-id" 2>/dev/null || echo "${HOSTNAME:-storage}")}"
NODE_CERT_FILE="${STORAGE_NODE_CERT_FILE:-/etc/opengitbase/node.crt}"
WATERMARK_DIR="${STORAGE_WATERMARK_DIR:-/var/lib/opengitbase/watermarks}"

if [ -z "${PHYSICAL_PATH}" ]; then
  echo "storage-quorum-replicate: physical path is required" >&2
  exit 1
fi

REPO_ID="$(basename "${PHYSICAL_PATH}" .git)"

if [ ! -r "${TOKEN_FILE}" ]; then
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

TOKEN="$(cat "${TOKEN_FILE}")"
CERT_THUMBPRINT="$(get_certificate_thumbprint)"
if [ -z "${CERT_THUMBPRINT}" ]; then
  echo "storage-quorum-replicate: certificate thumbprint unavailable" >&2
  exit 1
fi

CONTEXT="$(curl -fsS -m 30 "${API_URL}/api/v1/storage-nodes/repositories/${REPO_ID}/replication" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "X-Storage-Node-Id: ${NODE_ID}" \
  -H "X-Storage-Node-Certificate-Thumbprint: ${CERT_THUMBPRINT}")"

IS_PRIMARY="$(python3 -c 'import json,sys; print(json.load(sys.stdin).get("isPrimary", False))' <<< "${CONTEXT}")"
PRIMARY_WATERMARK="$(python3 -c 'import json,sys; print(json.load(sys.stdin).get("primaryWatermark", -1))' <<< "${CONTEXT}")"
REPLICATION_STATE="$(python3 -c 'import json,sys; print(json.load(sys.stdin).get("replicationState", ""))' <<< "${CONTEXT}")"
REPLICATION_EPOCH="$(python3 -c 'import json,sys; print(json.load(sys.stdin).get("replicationEpoch", 0))' <<< "${CONTEXT}")"

if [ "${IS_PRIMARY}" != "True" ]; then
  exit 0
fi

mkdir -p "${WATERMARK_DIR}"
WATERMARK_FILE="${WATERMARK_DIR}/${REPO_ID}.txt"
CURRENT="${PRIMARY_WATERMARK}"
NEW_WATERMARK=$((CURRENT + 1))
printf '%s' "${NEW_WATERMARK}" > "${WATERMARK_FILE}"

CONFIRMED_ENCRYPTED_JSON="[]"
if [ "${REPLICATION_STATE}" = "Rf4Healthy" ]; then
  KEY_JSON="$(curl -fsS -m 30 "${API_URL}/api/v1/storage-nodes/repositories/${REPO_ID}/repository-key" \
    -H "Authorization: Bearer ${TOKEN}" \
    -H "X-Storage-Node-Id: ${NODE_ID}" \
    -H "X-Storage-Node-Certificate-Thumbprint: ${CERT_THUMBPRINT}")"
  KEY_BASE64="$(python3 -c 'import json,sys; print(json.load(sys.stdin).get("keyBase64", ""))' <<< "${KEY_JSON}")"
  KEY_VERSION="$(python3 -c 'import json,sys; print(json.load(sys.stdin).get("keyVersion", 0))' <<< "${KEY_JSON}")"
  KEY_HEX="$(python3 -c 'import base64,sys; print(base64.b64decode(sys.argv[1]).hex())' "${KEY_BASE64}")"

  BUNDLE_FILE="$(mktemp)"
  cleanup() {
    rm -f "${BUNDLE_FILE}"
  }
  trap cleanup EXIT

  git -C "${PHYSICAL_PATH}" bundle create "${BUNDLE_FILE}" --all
  ARTIFACT_JSON="$(python3 /usr/local/bin/storage_artifact_crypto.py encrypt \
    "${BUNDLE_FILE}" "${KEY_HEX}" "${REPO_ID}" "${NEW_WATERMARK}" "${REPLICATION_EPOCH}" "${KEY_VERSION}")"

  ENCRYPTED_TARGET="$(python3 - <<'PY' "${CONTEXT}"
import json
import sys

context = json.loads(sys.argv[1])
peers = [
    peer
    for peer in context.get("peers", [])
    if peer.get("role") == "EncryptedReplica" and peer.get("isHealthy")
]
if not peers:
    sys.exit(1)
print(json.dumps(peers[0]))
PY
)"
  TARGET_HOST="$(python3 -c 'import json,sys; print(json.load(sys.stdin).get("internalHost", ""))' <<< "${ENCRYPTED_TARGET}")"
  TARGET_PORT="$(python3 -c 'import json,sys; print(json.load(sys.stdin).get("internalHttpPort", 8081))' <<< "${ENCRYPTED_TARGET}")"
  TARGET_NODE_ID="$(python3 -c 'import json,sys; print(json.load(sys.stdin).get("storageNodeId", ""))' <<< "${ENCRYPTED_TARGET}")"

  python3 - <<'PY' "${ARTIFACT_JSON}" "${API_URL}" "${REPO_ID}" "${NEW_WATERMARK}" "${TOKEN}" "${TARGET_HOST}" "${TARGET_PORT}"
import json
import sys
import urllib.request

artifact = json.loads(sys.argv[1])
api_url = sys.argv[2]
repo_id = sys.argv[3]
watermark = sys.argv[4]
token = sys.argv[5]
target_host = sys.argv[6]
target_port = sys.argv[7]

payload = json.dumps(
    {
        "manifest": artifact["manifest"],
        "bundleBase64": artifact["bundleHex"],
    }
).encode("utf-8")
request = urllib.request.Request(
    f"http://{target_host}:{target_port}/internal/repos/{repo_id}/artifacts/{watermark}",
    data=payload,
    method="PUT",
    headers={
        "Authorization": f"Bearer {token}",
        "Content-Type": "application/json",
    },
)
with urllib.request.urlopen(request, timeout=300) as response:
    if response.status not in (200, 201):
        raise RuntimeError(f"artifact upload failed with status {response.status}")
PY

  CONFIRMED_ENCRYPTED_JSON="[\"${TARGET_NODE_ID}\"]"
fi

# Do not use curl -f: API returns 409 with a JSON body on quorum failure.
RESPONSE="$(curl -sS -m 300 -X POST \
  "${API_URL}/api/v1/storage-nodes/repositories/${REPO_ID}/quorum-replicate" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -H "X-Storage-Node-Id: ${NODE_ID}" \
  -H "X-Storage-Node-Certificate-Thumbprint: ${CERT_THUMBPRINT}" \
  -d "{\"appliedWatermark\": ${NEW_WATERMARK}, \"confirmedEncryptedNodeIds\": ${CONFIRMED_ENCRYPTED_JSON}}")"

SUCCESS="$(python3 -c 'import json,sys; print(json.load(sys.stdin).get("success", False))' <<< "${RESPONSE}")"

if [ "${SUCCESS}" != "True" ]; then
  printf '%s' "${CURRENT}" > "${WATERMARK_FILE}"
  echo "storage-quorum-replicate: quorum replication failed" >&2
  exit 1
fi

if [ -n "${PUSH_REF}" ] && [ -n "${AFTER_SHA}" ]; then
  curl -sS -m 10 -X POST \
    "${API_URL}/api/v1/internal/pipelines/git-push-ingest" \
    -H "Content-Type: application/json" \
    -H "X-Storage-Node-Id: ${NODE_ID}" \
    -d "{\"repositoryId\":\"${REPO_ID}\",\"ref\":\"${PUSH_REF}\",\"afterSha\":\"${AFTER_SHA}\"}" >/dev/null || true
fi

exit 0
