#!/usr/bin/env bash
#
# Integration test: provision a bare repo via internal HTTP API (:8081),
# then git clone over Smart HTTP (:8082).
#
# Usage:
#   applications/repo-storage-layer/scripts/git-http-integration-test.sh

set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
APP_DIR=$(cd "${SCRIPT_DIR}/.." && pwd)
REPO_ROOT=$(cd "${APP_DIR}/../.." && pwd)
# shellcheck source=../../../scripts/docker-env.sh
source "${REPO_ROOT}/scripts/docker-env.sh"

IMAGE_TAG="opengitbase/repo-storage-layer:test"
API_TOKEN="git-http-integration-test-token"
REPO_ID="$(uuidgen | tr '[:upper:]' '[:lower:]')"
PHYSICAL_PATH="/srv/git/${REPO_ID}.git"

CONTAINER_NAME=""
TMP=""

require_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "error: required command not found: $1" >&2
    exit 1
  fi
}

cleanup() {
  if [ -n "${CONTAINER_NAME}" ]; then
    docker rm -f "${CONTAINER_NAME}" >/dev/null 2>&1 || true
  fi
  if [ -n "${TMP}" ] && [ -d "${TMP}" ]; then
    rm -rf "${TMP}"
  fi
}

wait_for_port() {
  local port=$1 attempt
  for attempt in $(seq 1 30); do
    if nc -z 127.0.0.1 "${port}" 2>/dev/null; then
      return 0
    fi
    sleep 1
  done
  echo "error: port ${port} did not become reachable" >&2
  return 1
}

wait_for_provision_api() {
  local port=$1 attempt code
  for attempt in $(seq 1 30); do
    code=$(curl -sS -o /dev/null -w "%{http_code}" \
      "http://127.0.0.1:${port}/internal/repos" 2>/dev/null || echo "000")
    if [ "${code}" = "401" ] || [ "${code}" = "501" ]; then
      return 0
    fi
    sleep 1
  done
  echo "error: provisioning API on port ${port} did not become ready" >&2
  return 1
}

wait_for_git_http() {
  local port=$1 attempt code
  for attempt in $(seq 1 30); do
    code=$(curl -sS -o /dev/null -w "%{http_code}" \
      "http://127.0.0.1:${port}/" 2>/dev/null || echo "000")
    if [ "${code}" = "404" ]; then
      return 0
    fi
    sleep 1
  done
  echo "error: git HTTP on port ${port} did not become ready" >&2
  return 1
}

require_cmd docker
require_cmd git
require_cmd curl
require_cmd nc
require_cmd uuidgen

TMP=$(mktemp -d)
trap cleanup EXIT

echo "==> Building Docker image ${IMAGE_TAG}"
docker build -t "${IMAGE_TAG}" "${APP_DIR}"

CONTAINER_NAME="repo-storage-git-http-it-${RANDOM}"
echo "==> Starting standalone container ${CONTAINER_NAME}"

docker run -d --name "${CONTAINER_NAME}" \
  -e STORAGE_STANDALONE=1 \
  -e STORAGE_API_TOKEN="${API_TOKEN}" \
  -p 0:8081 \
  -p 0:8082 \
  -v "${TMP}/repos:/srv/git" \
  "${IMAGE_TAG}" >/dev/null

PROVISION_PORT=$(docker port "${CONTAINER_NAME}" 8081/tcp | sed -E 's/.*:([0-9]+)$/\1/' | head -1)
GIT_HTTP_PORT=$(docker port "${CONTAINER_NAME}" 8082/tcp | sed -E 's/.*:([0-9]+)$/\1/' | head -1)
if [ -z "${PROVISION_PORT}" ] || [ -z "${GIT_HTTP_PORT}" ]; then
  echo "error: could not discover mapped HTTP ports" >&2
  docker logs "${CONTAINER_NAME}" >&2 || true
  exit 1
fi

echo "==> Waiting for provisioning API on 127.0.0.1:${PROVISION_PORT}"
wait_for_provision_api "${PROVISION_PORT}"
echo "==> Waiting for git HTTP on 127.0.0.1:${GIT_HTTP_PORT}"
wait_for_git_http "${GIT_HTTP_PORT}"

echo "==> Provisioning bare repository via internal API"
HTTP_STATUS=$(curl -sS -o /tmp/git-http-it-response.json -w "%{http_code}" \
  -X POST "http://127.0.0.1:${PROVISION_PORT}/internal/repos" \
  -H "Authorization: Bearer ${API_TOKEN}" \
  -H "Content-Type: application/json" \
  -d "{\"physicalPath\":\"${PHYSICAL_PATH}\"}")
if [ "${HTTP_STATUS}" != "201" ]; then
  echo "error: provisioning failed with status ${HTTP_STATUS}" >&2
  cat /tmp/git-http-it-response.json >&2 || true
  docker logs "${CONTAINER_NAME}" >&2 || true
  exit 1
fi

CLONE_URL="http://127.0.0.1:${GIT_HTTP_PORT}/${REPO_ID}.git"
echo "==> Cloning via git Smart HTTP (${CLONE_URL})"
for attempt in $(seq 1 5); do
  if git clone "${CLONE_URL}" "${TMP}/clone" >/dev/null 2>&1; then
    break
  fi
  if [ "${attempt}" -eq 5 ]; then
    echo "error: git clone failed after retries" >&2
    docker logs "${CONTAINER_NAME}" >&2 || true
    exit 1
  fi
  sleep 1
done

if [ ! -d "${TMP}/clone/.git" ]; then
  echo "error: clone did not produce a git repository" >&2
  exit 1
fi

echo "git-http integration test passed"
