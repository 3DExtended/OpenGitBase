#!/usr/bin/env bash
# Integration test: mTLS git-native peer sync between bare repos (ha-storage-03).
set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
APP_DIR=$(cd "${SCRIPT_DIR}/.." && pwd)
REPO_ROOT=$(cd "${APP_DIR}/../.." && pwd)
# shellcheck source=../../../scripts/docker-env.sh
source "${REPO_ROOT}/scripts/docker-env.sh"

IMAGE_TAG="opengitbase/repo-storage-layer:peer-sync-test"
CONTAINER_NAME=""
TMP=""

cleanup() {
  if [ -n "${CONTAINER_NAME}" ]; then
    docker rm -f "${CONTAINER_NAME}" >/dev/null 2>&1 || true
  fi
  if [ -n "${TMP}" ] && [ -d "${TMP}" ]; then
    rm -rf "${TMP}"
  fi
}

require_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "error: required command not found: $1" >&2
    exit 1
  fi
}

require_cmd docker
require_cmd curl

PKI_SCRIPT="${REPO_ROOT}/docker/pki/generate-node-pki.sh"
if [ -x "${PKI_SCRIPT}" ]; then
  "${PKI_SCRIPT}"
fi

TMP=$(mktemp -d)
trap cleanup EXIT

mkdir -p "${TMP}/repos" "${TMP}/pki"
cp "${REPO_ROOT}/docker/pki/ca.crt" "${TMP}/pki/ca.crt"
cp "${REPO_ROOT}/docker/pki/storage-1.crt" "${TMP}/pki/node.crt"
cp "${REPO_ROOT}/docker/pki/storage-1.key" "${TMP}/pki/node.key"

echo "==> Building storage image"
docker build -t "${IMAGE_TAG}" "${APP_DIR}"

CONTAINER_NAME="opengitbase_peer_sync_test_$$"
echo "==> Starting storage container"
docker run -d --name "${CONTAINER_NAME}" \
  --add-host storage-1:127.0.0.1 \
  -e STORAGE_STANDALONE=1 \
  -e STORAGE_API_TOKEN=test-token \
  -e STORAGE_HTTP_PORT=8081 \
  -e STORAGE_GIT_HTTP_PORT=8082 \
  -e STORAGE_MTLS_GIT_HTTP_PORT=8443 \
  -v "${TMP}/repos:/srv/git" \
  -v "${TMP}/pki/ca.crt:/etc/opengitbase/ca.crt:ro" \
  -v "${TMP}/pki/node.crt:/etc/opengitbase/node.crt:ro" \
  -v "${TMP}/pki/node.key:/etc/opengitbase/node.key:ro" \
  "${IMAGE_TAG}" >/dev/null

for _ in $(seq 1 30); do
  if docker exec "${CONTAINER_NAME}" curl -fsS -o /dev/null \
    -H "Authorization: Bearer test-token" \
    -X POST -H "Content-Type: application/json" \
    -d '{}' "http://127.0.0.1:8081/internal/repos" 2>/dev/null; then
    break
  fi
  sleep 1
done

SOURCE_PATH="/srv/git/source.git"
TARGET_PATH="/srv/git/target.git"

echo "==> Provisioning source and target bare repos"
docker exec "${CONTAINER_NAME}" curl -fsS -X POST "http://127.0.0.1:8081/internal/repos" \
  -H "Authorization: Bearer test-token" \
  -H "Content-Type: application/json" \
  -d "{\"physicalPath\":\"${SOURCE_PATH}\"}" >/dev/null

docker exec "${CONTAINER_NAME}" curl -fsS -X POST "http://127.0.0.1:8081/internal/repos" \
  -H "Authorization: Bearer test-token" \
  -H "Content-Type: application/json" \
  -d "{\"physicalPath\":\"${TARGET_PATH}\"}" >/dev/null

echo "==> Seeding source repo with a commit (inside container as git user)"
docker exec -u git "${CONTAINER_NAME}" bash -lc '
  set -euo pipefail
  WORK=/tmp/peer-sync-work
  rm -rf "$WORK"
  git init -b main "$WORK"
  echo "peer sync test" > "$WORK/README.md"
  git -C "$WORK" add README.md
  git -C "$WORK" -c user.email=test@example.com -c user.name=test commit -m "seed"
  git -C "$WORK" push "http://127.0.0.1:8082/source.git" HEAD:main
'

echo "==> Syncing target from source over mTLS git HTTP"
docker exec "${CONTAINER_NAME}" curl -fsS -X POST "http://127.0.0.1:8081/internal/repos/sync-from" \
  -H "Authorization: Bearer test-token" \
  -H "Content-Type: application/json" \
  -d "{\"physicalPath\":\"${TARGET_PATH}\",\"sourcePhysicalPath\":\"${SOURCE_PATH}\",\"sourceHost\":\"storage-1\",\"sourcePort\":8443}" >/dev/null

SOURCE_SHA=$(docker exec "${CONTAINER_NAME}" git --git-dir="${SOURCE_PATH}" rev-parse refs/heads/main)
TARGET_SHA=$(docker exec "${CONTAINER_NAME}" git --git-dir="${TARGET_PATH}" rev-parse refs/heads/main)

if [ "${SOURCE_SHA}" != "${TARGET_SHA}" ]; then
  echo "error: ref mismatch after sync (source=${SOURCE_SHA}, target=${TARGET_SHA})" >&2
  exit 1
fi

echo "OK: peer mTLS replication synced refs (${SOURCE_SHA})"
