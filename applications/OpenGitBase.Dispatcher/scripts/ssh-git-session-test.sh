#!/usr/bin/env bash
#
# End-to-end test for SSH git through the dispatcher: an SSH client logs in to the dispatcher's
# sshd, the auth hook and the git session ask a stub API, and the session proxies the git
# command to a stub storage node with the storage SSH key the entrypoint bootstrapped.
#
# Covers what only shows up under sshd: sshd starts AuthorizedKeysCommand and sessions with a
# minimal environment (no container env vars), and runs the session as the git user.
#
# Usage:
#   applications/OpenGitBase.Dispatcher/scripts/ssh-git-session-test.sh
#
# Requires: docker, jq, ssh-keygen, mktemp

set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
APP_DIR=$(cd "${SCRIPT_DIR}/.." && pwd)
REPO_ROOT=$(cd "${APP_DIR}/../.." && pwd)
# shellcheck source=../../../scripts/docker-env.sh
source "${REPO_ROOT}/scripts/docker-env.sh"

IMAGE_TAG="opengitbase/dispatcher:ssh-session-test"
STUB_IMAGE="haproxy:3.2"
KEY_PATH="/run/secrets/dispatcher_storage_ssh"
RUN_ID="ogb-dispatcher-ssh-$$"
NETWORK="${RUN_ID}"
API_NAME="${RUN_ID}-api"
STORAGE_NAME="${RUN_ID}-storage"
DISPATCHER_NAME="${RUN_ID}-dispatcher"
STUB_API_URL="http://${API_NAME}:8080"
PHYSICAL_PATH="/srv/git/demo-id.git"
WAIT_SECONDS="${WAIT_SECONDS:-90}"

# Set during the run; used by the EXIT trap for cleanup.
CONTAINERS=()
NETWORK_CREATED=false
TMP=""
FAILURES=0

require_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "error: required command not found: $1" >&2
    exit 1
  fi
}

cleanup() {
  local name
  for name in "${CONTAINERS[@]+"${CONTAINERS[@]}"}"; do
    docker rm -f "${name}" >/dev/null 2>&1 || true
  done
  if [ "${NETWORK_CREATED}" = "true" ]; then
    docker network rm "${NETWORK}" >/dev/null 2>&1 || true
  fi
  if [ -n "${TMP}" ] && [ -d "${TMP}" ]; then
    rm -rf "${TMP}"
  fi
}
trap cleanup EXIT

pass() {
  echo "    ok: $*"
}

fail() {
  echo "    FAIL: $*" >&2
  FAILURES=$((FAILURES + 1))
}

# Captured whole: `docker logs | grep -q` fails under pipefail when grep exits early.
container_logs() {
  docker logs "$1" 2>&1 || true
}

show_logs() {
  echo "    --- $1 logs" >&2
  container_logs "$1" | tail -n 30 | sed 's/^/      /' >&2
}

wait_for_key_installed() {
  local i
  for i in $(seq 1 "${WAIT_SECONDS}"); do
    if grep -q -E "^entrypoint: (installed|WARNING)" <<<"$(container_logs "${DISPATCHER_NAME}")"; then
      grep -q "^entrypoint: installed" <<<"$(container_logs "${DISPATCHER_NAME}")"
      return
    fi
    sleep 1
  done
  return 1
}

wait_for_sshd() {
  local i
  for i in $(seq 1 "${WAIT_SECONDS}"); do
    if docker exec "${STORAGE_NAME}" bash -c "exec 3<>/dev/tcp/${DISPATCHER_NAME}/22" 2>/dev/null; then
      return 0
    fi
    sleep 1
  done
  return 1
}

require_cmd docker
require_cmd jq
require_cmd ssh-keygen
require_cmd mktemp

TMP=$(mktemp -d)
mkdir -p "${TMP}/stub"

echo "==> Generating throwaway keys and the stub API responses"
# Dispatcher -> storage key, served by the bootstrap endpoint.
ssh-keygen -q -t ed25519 -N '' -C "storage-key" -f "${TMP}/stub/storage_key"
# The git user's key that logs in to the dispatcher.
ssh-keygen -q -t ed25519 -N '' -C "user-key" -f "${TMP}/stub/user_key"
user_public_key=$(cut -d' ' -f1,2 "${TMP}/stub/user_key.pub")
user_fingerprint=$(ssh-keygen -l -E sha256 -f "${TMP}/stub/user_key.pub" | cut -d' ' -f2)

jq -n --rawfile key "${TMP}/stub/storage_key" '{privateKey: $key}' >"${TMP}/stub/bootstrap.json"
jq -n --arg key "${user_public_key}" --arg fp "${user_fingerprint}" \
  '{authorizedKeysLine: ("environment=\"SSH_KEY_FINGERPRINT=" + $fp + "\" " + $key), publicSshKey: $key}' \
  >"${TMP}/stub/by-fingerprint.json"
jq -n --arg host "${STORAGE_NAME}" --arg path "${PHYSICAL_PATH}" \
  '{allowed: true, physicalPath: $path, storageNodeInternalHost: $host, storageNodeInternalSshPort: 22, storageNodeInternalGitHttpPort: 8082}' \
  >"${TMP}/stub/access-check.json"
echo '{}' >"${TMP}/stub/empty.json"

cat >"${TMP}/stub/haproxy.cfg" <<'CFG'
global
  log stdout format raw local0

defaults
  mode http
  log global
  option httplog
  timeout connect 2s
  timeout client 10s
  timeout server 10s

frontend api
  bind :8080
  acl bootstrap path /api/v1/fleet/bootstrap/dispatcher-ssh-private-key
  http-request return status 200 content-type application/json file /stub/bootstrap.json if bootstrap { req.hdr(X-Fleet-Bootstrap-Token) -m str valid-token }
  http-request return status 401 if bootstrap
  http-request return status 200 content-type application/json file /stub/by-fingerprint.json if { path /api/v1/ssh-authentication/by-fingerprint }
  http-request return status 200 content-type application/json file /stub/access-check.json if { path /api/v1/access-checks/repositories }
  http-request return status 200 content-type application/json file /stub/empty.json if { path_beg /api/v1/internal/fleet-components/ }
  http-request return status 404
CFG

# Storage node stand-in: sshd that only accepts the storage key and echoes the command it got.
cat "${TMP}/stub/storage_key.pub" >"${TMP}/stub/authorized_keys"
cat >"${TMP}/stub/storage_sshd_config" <<'CFG'
Port 22
HostKey /etc/ssh/ssh_host_ed25519_key
UsePAM no
PermitRootLogin no
PasswordAuthentication no
KbdInteractiveAuthentication no
PubkeyAuthentication yes
AuthorizedKeysFile .ssh/authorized_keys
AllowUsers git
PidFile /var/run/sshd.pid
ForceCommand echo "storage reached: ${SSH_ORIGINAL_COMMAND}"
CFG
chmod 755 "${TMP}/stub"
chmod 644 "${TMP}/stub/"*

echo "==> Building ${IMAGE_TAG}"
docker build -q -t "${IMAGE_TAG}" "${APP_DIR}" >/dev/null

docker network create "${NETWORK}" >/dev/null
NETWORK_CREATED=true

echo "==> Starting the stub API and the stub storage node"
docker run -d --name "${API_NAME}" --network "${NETWORK}" \
  -v "${TMP}/stub:/stub:ro" \
  "${STUB_IMAGE}" haproxy -f /stub/haproxy.cfg >/dev/null
CONTAINERS+=("${API_NAME}")

docker run -d --name "${STORAGE_NAME}" --network "${NETWORK}" \
  -v "${TMP}/stub:/stub:ro" \
  --entrypoint bash "${IMAGE_TAG}" -c '
    install -d -o git -g git -m 700 /home/git/.ssh
    install -o git -g git -m 600 /stub/authorized_keys /home/git/.ssh/authorized_keys
    install -m 600 /stub/user_key /root/user_key
    exec /usr/sbin/sshd -D -e -f /stub/storage_sshd_config' >/dev/null
CONTAINERS+=("${STORAGE_NAME}")

echo "==> Starting the dispatcher with SSH git enabled (environment as in docker-compose.yml)"
docker run -d --name "${DISPATCHER_NAME}" --network "${NETWORK}" \
  -e DISPATCHER_API_URL="${STUB_API_URL}" \
  -e Dispatcher__ApiUrl="${STUB_API_URL}" \
  -e Dispatcher__DispatcherId="${DISPATCHER_NAME}" \
  -e Dispatcher__HttpPort=8082 \
  -e Dispatcher__StorageSshPrivateKeyPath="${KEY_PATH}" \
  -e Dispatcher__StorageSshUser=git \
  -e GIT_SSH_ENABLED=true \
  -e FLEET_BOOTSTRAP_TOKEN=valid-token \
  "${IMAGE_TAG}" >/dev/null
CONTAINERS+=("${DISPATCHER_NAME}")

if wait_for_key_installed; then
  pass "entrypoint installed the storage SSH key"
else
  fail "entrypoint did not install the storage SSH key"
  show_logs "${DISPATCHER_NAME}"
fi

if wait_for_sshd; then
  pass "dispatcher sshd accepts connections"
else
  fail "dispatcher sshd does not accept connections"
  show_logs "${DISPATCHER_NAME}"
fi

echo "==> git user can read the storage SSH key"
if docker exec -u git "${DISPATCHER_NAME}" cat "${KEY_PATH}" 2>/dev/null | cmp -s - "${TMP}/stub/storage_key"; then
  pass "git reads the key byte-for-byte"
else
  fail "git cannot read ${KEY_PATH}: $(docker exec -u git "${DISPATCHER_NAME}" cat "${KEY_PATH}" 2>&1 >/dev/null | head -n 1)"
fi

echo "==> SSH git session reaches storage"
set +e
session_output=$(docker exec "${STORAGE_NAME}" ssh -i /root/user_key \
  -o BatchMode=yes -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -o LogLevel=ERROR \
  -o ConnectTimeout=10 \
  "git@${DISPATCHER_NAME}" "git-upload-pack 'alice/demo.git'" </dev/null 2>&1)
session_exit=$?
set -e
expected="storage reached: git-upload-pack '${PHYSICAL_PATH}'"
if [ "${session_exit}" = "0" ] && grep -q -F "${expected}" <<<"${session_output}"; then
  pass "session proxied to storage with the bootstrapped key"
else
  fail "session exit ${session_exit}, want 0 and '${expected}'"
  sed 's/^/      /' <<<"${session_output}" >&2
  show_logs "${DISPATCHER_NAME}"
  show_logs "${API_NAME}"
fi

if [ "${FAILURES}" -gt 0 ]; then
  echo "FAILED: ${FAILURES} check(s)" >&2
  exit 1
fi
echo "All dispatcher SSH git session checks passed."
