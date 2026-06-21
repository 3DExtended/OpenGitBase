#!/usr/bin/env bash
# Copyright (c) 2026 OpenGitBase Authors
# SPDX-License-Identifier: LicenseRef-OpenGitBase-1.0

set -euo pipefail

PHYSICAL_PATH="${1:-}"
CONFIG_DIR="${STORAGE_CONFIG_DIR:-/var/lib/opengitbase}"
API_URL="${STORAGE_API_URL:-$(cat "${CONFIG_DIR}/api-url" 2>/dev/null || echo http://api-lb:8080)}"
TOKEN_FILE="${STORAGE_TOKEN_FILE:-${CONFIG_DIR}/api-token}"
NODE_ID="${STORAGE_NODE_ID:-$(cat "${CONFIG_DIR}/node-id" 2>/dev/null || echo "${HOSTNAME:-storage}")}"
NODE_CERT_FILE="${STORAGE_NODE_CERT_FILE:-/etc/opengitbase/node.crt}"
WATERMARK_DIR="${STORAGE_WATERMARK_DIR:-/var/lib/opengitbase/watermarks}"
DEBUG_LOG_PATH="${DEBUG_LOG_PATH:-/tmp/debug-6f497e.log}"

#region agent log
_debug_log() {
  local hypothesis_id="$1"
  local location="$2"
  local message="$3"
  local data="$4"
  python3 - <<PY || true
import json, time
payload = {
    "sessionId": "6f497e",
    "hypothesisId": "${hypothesis_id}",
    "location": "${location}",
    "message": "${message}",
    "data": json.loads('''${data}'''),
    "timestamp": int(time.time() * 1000),
}
line = json.dumps(payload)
for path in ("${DEBUG_LOG_PATH}", "/Users/peteresser/Developer/projects/opengitbase/.cursor/debug-6f497e.log"):
    try:
        with open(path, "a", encoding="utf-8") as handle:
            handle.write(line + "\n")
    except OSError:
        pass
PY
}
#endregion

if [ -z "${PHYSICAL_PATH}" ]; then
  echo "storage-quorum-replicate: physical path is required" >&2
  exit 1
fi

REPO_ID="$(basename "${PHYSICAL_PATH}" .git)"
#region agent log
_debug_log "H1" "storage-quorum-replicate.sh:entry" "post-receive hook invoked" "{\"repoId\":\"${REPO_ID}\",\"physicalPath\":\"${PHYSICAL_PATH}\"}"
#endregion

if [ ! -r "${TOKEN_FILE}" ]; then
  #region agent log
  _debug_log "H2" "storage-quorum-replicate.sh:token" "API token unreadable" "{\"repoId\":\"${REPO_ID}\",\"tokenFile\":\"${TOKEN_FILE}\"}"
  #endregion
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
  #region agent log
  _debug_log "H2" "storage-quorum-replicate.sh:cert" "certificate thumbprint unavailable" "{\"repoId\":\"${REPO_ID}\"}"
  #endregion
  echo "storage-quorum-replicate: certificate thumbprint unavailable" >&2
  exit 1
fi

CONTEXT="$(curl -fsS "${API_URL}/api/v1/storage-nodes/repositories/${REPO_ID}/replication" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "X-Storage-Node-Id: ${NODE_ID}" \
  -H "X-Storage-Node-Certificate-Thumbprint: ${CERT_THUMBPRINT}")"

IS_PRIMARY="$(python3 -c 'import json,sys; print(json.load(sys.stdin).get("isPrimary", False))' <<< "${CONTEXT}")"
PRIMARY_WATERMARK="$(python3 -c 'import json,sys; print(json.load(sys.stdin).get("primaryWatermark", -1))' <<< "${CONTEXT}")"
IS_PRIMARY_JSON="$(python3 -c 'import json,sys; print("true" if json.load(sys.stdin).get("isPrimary") else "false")' <<< "${CONTEXT}")"
#region agent log
_debug_log "H3" "storage-quorum-replicate.sh:context" "replication context loaded" "{\"repoId\":\"${REPO_ID}\",\"isPrimary\":${IS_PRIMARY_JSON},\"primaryWatermark\":${PRIMARY_WATERMARK}}"
#endregion

if [ "${IS_PRIMARY}" != "True" ]; then
  #region agent log
  _debug_log "H3" "storage-quorum-replicate.sh:non-primary" "skipping quorum replicate on non-primary" "{\"repoId\":\"${REPO_ID}\"}"
  #endregion
  exit 0
fi

mkdir -p "${WATERMARK_DIR}"
WATERMARK_FILE="${WATERMARK_DIR}/${REPO_ID}.txt"
CURRENT="${PRIMARY_WATERMARK}"
NEW_WATERMARK=$((CURRENT + 1))
printf '%s' "${NEW_WATERMARK}" > "${WATERMARK_FILE}"
#region agent log
_debug_log "H4" "storage-quorum-replicate.sh:watermark" "watermark incremented from API context" "{\"repoId\":\"${REPO_ID}\",\"current\":${CURRENT},\"newWatermark\":${NEW_WATERMARK}}"
#endregion

RESPONSE="$(curl -fsS -m 300 -X POST \
  "${API_URL}/api/v1/storage-nodes/repositories/${REPO_ID}/quorum-replicate" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -H "X-Storage-Node-Id: ${NODE_ID}" \
  -H "X-Storage-Node-Certificate-Thumbprint: ${CERT_THUMBPRINT}" \
  -d "{\"appliedWatermark\": ${NEW_WATERMARK}}")"

SUCCESS="$(python3 -c 'import json,sys; print(json.load(sys.stdin).get("success", False))' <<< "${RESPONSE}")"
ERROR="$(python3 -c 'import json,sys; print(json.load(sys.stdin).get("error", ""))' <<< "${RESPONSE}")"
COMMITTED_WATERMARK="$(python3 -c 'import json,sys; print(json.load(sys.stdin).get("primaryWatermark", -1))' <<< "${RESPONSE}")"
#region agent log
_debug_log "H4" "storage-quorum-replicate.sh:quorum" "quorum replicate response" "{\"repoId\":\"${REPO_ID}\",\"success\":${SUCCESS},\"error\":\"${ERROR}\",\"committedWatermark\":${COMMITTED_WATERMARK}}"
#endregion

if [ "${SUCCESS}" != "True" ]; then
  printf '%s' "${CURRENT}" > "${WATERMARK_FILE}"
  echo "storage-quorum-replicate: quorum replication failed" >&2
  exit 1
fi

exit 0
