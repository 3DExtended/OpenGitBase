#!/usr/bin/env bash
#
# Regression test for the dispatcher entrypoint's storage SSH key bootstrap: build the
# dispatcher image, answer the fleet bootstrap call from an HAProxy stub, and check that
# a good response lands byte-for-byte while every failure leaves no key file behind and
# still serves Smart HTTP without restarting (no crash loop).
#
# Usage:
#   applications/OpenGitBase.Dispatcher/scripts/entrypoint-key-bootstrap-test.sh
#
# Requires: docker, jq, ssh-keygen, mktemp

set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
APP_DIR=$(cd "${SCRIPT_DIR}/.." && pwd)
REPO_ROOT=$(cd "${APP_DIR}/../.." && pwd)
# shellcheck source=../../../scripts/docker-env.sh
source "${REPO_ROOT}/scripts/docker-env.sh"

IMAGE_TAG="opengitbase/dispatcher:entrypoint-test"
STUB_IMAGE="haproxy:3.2"
KEY_PATH="/run/secrets/dispatcher_storage_ssh"
RUN_ID="ogb-dispatcher-entrypoint-$$"
NETWORK="${RUN_ID}"
STUB_API_URL="http://${RUN_ID}-api:8080"
# Nothing listens here, so curl fails to connect.
UNREACHABLE_API_URL="http://127.0.0.1:9"
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

# Creates (does not start) a dispatcher container whose entrypoint fetches the key with the given
# token from bootstrap_url. The .NET dispatcher always talks to the stub: it exits when its API is
# unreachable at startup, which is not what this test covers.
create_dispatcher() {
  local name="$1" token="$2" bootstrap_url="$3"
  docker create --name "${name}" --network "${NETWORK}" \
    -e FLEET_BOOTSTRAP_TOKEN="${token}" \
    -e DISPATCHER_API_URL="${bootstrap_url}" \
    -e Dispatcher__ApiUrl="${STUB_API_URL}" \
    -e Dispatcher__DispatcherId="${name}" \
    -e Dispatcher__StorageSshPrivateKeyPath="${KEY_PATH}" \
    -e GIT_SSH_ENABLED=false \
    "${IMAGE_TAG}" >/dev/null
  CONTAINERS+=("${name}")
}

is_running() {
  [ "$(docker inspect -f '{{.State.Running}}' "$1" 2>/dev/null)" = "true" ]
}

# Captured whole: `docker logs | grep -q` fails under pipefail when grep exits early.
container_logs() {
  docker logs "$1" 2>&1 || true
}

show_logs() {
  container_logs "$1" | sed -n '1,25{s/^/      /;p;}' >&2
}

# Waits until the entrypoint logs the bootstrap outcome. Fails early when the container stops.
wait_for_bootstrap_outcome() {
  local name="$1" i
  for i in $(seq 1 "${WAIT_SECONDS}"); do
    if grep -q -E "^entrypoint: (installed|WARNING)" <<<"$(container_logs "${name}")"; then
      return 0
    fi
    if ! is_running "${name}"; then
      return 1
    fi
    sleep 1
  done
  return 1
}

wait_for_http() {
  local name="$1" i
  for i in $(seq 1 "${WAIT_SECONDS}"); do
    if docker exec "${name}" curl -fsS http://127.0.0.1:8082/health >/dev/null 2>&1; then
      return 0
    fi
    sleep 1
  done
  return 1
}

# Prints missing, empty or present for the key file inside a running container.
key_file_state() {
  docker exec "$1" sh -c \
    'if [ ! -e "$1" ]; then echo missing; elif [ ! -s "$1" ]; then echo empty; else echo present; fi' \
    sh "${KEY_PATH}" 2>/dev/null || echo "unknown (container stopped)"
}

# Files in the key directory other than the key itself, e.g. leftover temp files.
stray_key_dir_files() {
  docker exec "$1" sh -c 'ls -A "$(dirname "$1")" 2>/dev/null | grep -v -x "$(basename "$1")" || true' \
    sh "${KEY_PATH}" 2>/dev/null || echo "unknown (container stopped)"
}

# A failed bootstrap must leave no key file and keep serving Smart HTTP without a restart.
assert_failed_bootstrap() {
  local name="$1" expected_log="$2" state stray restarts
  if ! wait_for_bootstrap_outcome "${name}"; then
    fail "${name}: no bootstrap outcome logged (running=$(is_running "${name}" && echo yes || echo no))"
    show_logs "${name}"
    return
  fi
  if wait_for_http "${name}"; then
    pass "${name}: Smart HTTP answers /health"
  else
    fail "${name}: Smart HTTP does not answer /health"
  fi
  restarts=$(docker inspect -f '{{.RestartCount}}' "${name}")
  if is_running "${name}" && [ "${restarts}" = "0" ]; then
    pass "${name}: still running, no restart"
  else
    fail "${name}: running=$(is_running "${name}" && echo yes || echo no) restarts=${restarts}"
  fi
  state=$(key_file_state "${name}")
  if [ "${state}" = "missing" ]; then
    pass "${name}: no key file written"
  else
    fail "${name}: key file is ${state}, want missing"
  fi
  stray=$(stray_key_dir_files "${name}")
  if [ -z "${stray}" ]; then
    pass "${name}: no temp files left"
  else
    fail "${name}: temp files left: ${stray}"
  fi
  if grep -q -F "${expected_log}" <<<"$(container_logs "${name}")"; then
    pass "${name}: logs '${expected_log}'"
  else
    fail "${name}: log does not mention '${expected_log}'"
    show_logs "${name}"
  fi
}

assert_installed_key() {
  local name="$1" mode stray
  if ! wait_for_bootstrap_outcome "${name}"; then
    fail "${name}: no bootstrap outcome logged (running=$(is_running "${name}" && echo yes || echo no))"
    show_logs "${name}"
    return
  fi
  if wait_for_http "${name}"; then
    pass "${name}: Smart HTTP answers /health"
  else
    fail "${name}: Smart HTTP does not answer /health"
  fi
  if docker exec "${name}" cat "${KEY_PATH}" | cmp -s - "${TMP}/key"; then
    pass "${name}: key file matches the served key byte-for-byte"
  else
    fail "${name}: key file differs from the served key"
  fi
  mode=$(docker exec "${name}" stat -c '%a' "${KEY_PATH}" 2>/dev/null || echo unknown)
  if [ "${mode}" = "600" ]; then
    pass "${name}: key file mode 600"
  else
    fail "${name}: key file mode ${mode}, want 600"
  fi
  stray=$(stray_key_dir_files "${name}")
  if [ -z "${stray}" ]; then
    pass "${name}: no temp files left"
  else
    fail "${name}: temp files left: ${stray}"
  fi
}

require_cmd docker
require_cmd jq
require_cmd ssh-keygen
require_cmd mktemp

TMP=$(mktemp -d)
mkdir -p "${TMP}/stub" "${TMP}/empty-key/secrets"

echo "==> Generating a throwaway storage SSH key and the stub responses"
ssh-keygen -q -t ed25519 -N '' -C "entrypoint-test" -f "${TMP}/key"
jq -n --rawfile key "${TMP}/key" '{privateKey: $key}' >"${TMP}/stub/valid.json"
# The corruption the old EncryptEmail path produced: the key lower-cased.
jq -n --rawfile key "${TMP}/key" '{privateKey: ($key | ascii_downcase)}' >"${TMP}/stub/corrupt-key.json"
echo '<html><body>Bad Gateway</body></html>' >"${TMP}/stub/not-json.html"
cat >"${TMP}/stub/haproxy.cfg" <<'CFG'
global
  log stdout format raw local0

defaults
  mode http
  timeout connect 2s
  timeout client 10s
  timeout server 10s

frontend api
  bind :8080
  acl bootstrap path /api/v1/fleet/bootstrap/dispatcher-ssh-private-key
  http-request return status 200 content-type application/json file /stub/valid.json if bootstrap { req.hdr(X-Fleet-Bootstrap-Token) -m str valid-token }
  http-request return status 200 content-type application/json file /stub/corrupt-key.json if bootstrap { req.hdr(X-Fleet-Bootstrap-Token) -m str corrupt-key-token }
  http-request return status 200 content-type text/html file /stub/not-json.html if bootstrap { req.hdr(X-Fleet-Bootstrap-Token) -m str not-json-token }
  http-request return status 401 if bootstrap
  http-request return status 404
CFG
chmod 755 "${TMP}/stub"
chmod 644 "${TMP}/stub/"*
# An empty key file as the old entrypoint left it after a failed fetch.
: >"${TMP}/empty-key/secrets/dispatcher_storage_ssh"

echo "==> Building ${IMAGE_TAG}"
docker build -q -t "${IMAGE_TAG}" "${APP_DIR}" >/dev/null

echo "==> Starting the stub API"
docker network create "${NETWORK}" >/dev/null
NETWORK_CREATED=true
docker run -d --name "${RUN_ID}-api" --network "${NETWORK}" \
  -v "${TMP}/stub:/stub:ro" \
  "${STUB_IMAGE}" haproxy -f /stub/haproxy.cfg >/dev/null
CONTAINERS+=("${RUN_ID}-api")

echo "==> Starting one dispatcher per case"
create_dispatcher "${RUN_ID}-valid" valid-token "${STUB_API_URL}"
create_dispatcher "${RUN_ID}-used-token" used-token "${STUB_API_URL}"
create_dispatcher "${RUN_ID}-not-json" not-json-token "${STUB_API_URL}"
create_dispatcher "${RUN_ID}-corrupt-key" corrupt-key-token "${STUB_API_URL}"
create_dispatcher "${RUN_ID}-unreachable" valid-token "${UNREACHABLE_API_URL}"
create_dispatcher "${RUN_ID}-empty-file" valid-token "${STUB_API_URL}"
docker cp "${TMP}/empty-key/secrets" "${RUN_ID}-empty-file:/run/" >/dev/null
for name in "${CONTAINERS[@]}"; do
  if [ "${name}" != "${RUN_ID}-api" ]; then
    docker start "${name}" >/dev/null
  fi
done

echo "==> Valid response: key installed"
assert_installed_key "${RUN_ID}-valid"

echo "==> Token already used (HTTP 401)"
assert_failed_bootstrap "${RUN_ID}-used-token" "HTTP 401"

echo "==> Response is not JSON"
assert_failed_bootstrap "${RUN_ID}-not-json" "no privateKey"

echo "==> Response holds a corrupt key"
assert_failed_bootstrap "${RUN_ID}-corrupt-key" "not a valid private key"

echo "==> API unreachable"
assert_failed_bootstrap "${RUN_ID}-unreachable" "could not reach"

echo "==> Empty key file from an earlier failed fetch is fetched again"
assert_installed_key "${RUN_ID}-empty-file"

if [ "${FAILURES}" -gt 0 ]; then
  echo "FAILED: ${FAILURES} check(s)" >&2
  exit 1
fi
echo "All dispatcher entrypoint key bootstrap checks passed."
