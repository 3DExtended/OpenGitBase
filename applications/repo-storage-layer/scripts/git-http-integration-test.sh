#!/usr/bin/env bash
#
# Integration test: provision a bare repo via internal HTTP API (:8081),
# then git clone over Smart HTTP (:8082); import a bundle as cold recovery
# does and check the imported repository takes pushes.
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
require_cmd openssl

TMP=$(mktemp -d)
trap cleanup EXIT

echo "==> Building Docker image ${IMAGE_TAG}"
docker build -t "${IMAGE_TAG}" "${APP_DIR}"

# nginx serves :8082 and the peer mTLS port :8443 from one config; without node certificates it does not start at all.
mkdir -p "${TMP}/tls"
openssl req -x509 -newkey rsa:2048 -nodes -subj /CN=git-http-it -days 1 \
  -keyout "${TMP}/tls/node.key" -out "${TMP}/tls/node.crt" 2>/dev/null
cp "${TMP}/tls/node.crt" "${TMP}/tls/ca.crt"

CONTAINER_NAME="repo-storage-git-http-it-${RANDOM}"
echo "==> Starting standalone container ${CONTAINER_NAME}"

docker run -d --name "${CONTAINER_NAME}" \
  -e STORAGE_STANDALONE=1 \
  -e STORAGE_API_TOKEN="${API_TOKEN}" \
  -p 0:8081 \
  -p 0:8082 \
  -v "${TMP}/repos:/srv/git" \
  -v "${TMP}/tls/node.crt:/etc/opengitbase/node.crt:ro" \
  -v "${TMP}/tls/node.key:/etc/opengitbase/node.key:ro" \
  -v "${TMP}/tls/ca.crt:/etc/opengitbase/ca.crt:ro" \
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

echo "==> Importing a bundle the way cold recovery does"
IMPORT_ID="$(uuidgen | tr '[:upper:]' '[:lower:]')"
git -C "${TMP}/clone" symbolic-ref HEAD refs/heads/main
git -C "${TMP}/clone" -c user.name=t -c user.email=t@example.com commit -q --allow-empty -m init
# On main's commit: the clone inside import-bundle guesses HEAD from the commit, and the image's git picks this one.
git -C "${TMP}/clone" branch release
git -C "${TMP}/clone" bundle create -q "${TMP}/import.bundle" --all
printf '{"physicalPath":"/srv/git/%s.git","bundleBase64":"%s"}' \
  "${IMPORT_ID}" "$(base64 < "${TMP}/import.bundle" | tr -d '\n')" > "${TMP}/import.json"
HTTP_STATUS=$(curl -sS -o "${TMP}/import-response.json" -w "%{http_code}" \
  -X POST "http://127.0.0.1:${PROVISION_PORT}/internal/repos/import-bundle" \
  -H "Authorization: Bearer ${API_TOKEN}" \
  -H "Content-Type: application/json" \
  --data @"${TMP}/import.json")
if [ "${HTTP_STATUS}" != "201" ]; then
  echo "error: import-bundle failed with status ${HTTP_STATUS}" >&2
  cat "${TMP}/import-response.json" >&2 || true
  docker logs "${CONTAINER_NAME}" >&2 || true
  exit 1
fi

# A push asks for the receive-pack advertisement first. The non-bare clone import-bundle used to make answered 403.
IMPORT_URL="http://127.0.0.1:${GIT_HTTP_PORT}/${IMPORT_ID}.git"
HTTP_STATUS=$(curl -sS -o /dev/null -w "%{http_code}" "${IMPORT_URL}/info/refs?service=git-receive-pack")
if [ "${HTTP_STATUS}" != "200" ]; then
  echo "error: the imported repository refuses pushes (receive-pack info/refs answered ${HTTP_STATUS})" >&2
  exit 1
fi
IMPORTED_BRANCHES=$(git ls-remote --heads "${IMPORT_URL}" | awk '{print $2}' | sort | tr '\n' ' ')
if [ "${IMPORTED_BRANCHES}" != "refs/heads/main refs/heads/release " ]; then
  echo "error: the imported repository has branches '${IMPORTED_BRANCHES}', expected main and release" >&2
  exit 1
fi
IMPORTED_HEAD=$(git ls-remote --symref "${IMPORT_URL}" HEAD | awk '/^ref:/ {print $2}')
if [ "${IMPORTED_HEAD}" != "refs/heads/main" ]; then
  echo "error: the imported repository's HEAD is '${IMPORTED_HEAD}', expected refs/heads/main" >&2
  exit 1
fi

echo "git-http integration test passed"
