#!/usr/bin/env bash
# Start Smart HTTP, fetch the dispatcher SSH private key from API fleet bootstrap, and start optional sshd.
set -euo pipefail

API_URL="${DISPATCHER_API_URL:-http://api:8080}"
FLEET_BOOTSTRAP_TOKEN="${FLEET_BOOTSTRAP_TOKEN:-}"
PRIVATE_KEY_PATH="${DISPATCHER_STORAGE_SSH_KEY_PATH:-/run/secrets/dispatcher_storage_ssh}"
GIT_SSH_ENABLED="${GIT_SSH_ENABLED:-false}"

log() {
  echo "entrypoint: $*" >&2
}

# Fetches the key into a temp dir next to PRIVATE_KEY_PATH and moves it into place only when the
# response parses and ssh-keygen accepts the key, so a failed fetch never leaves a file that the
# next start would take for a key. Runs in a subshell: errexit is off when called from `if`, so
# every step checks its own status, and the EXIT trap removes the temp dir.
fetch_storage_ssh_key() (
  umask 077
  key_dir="$(dirname "${PRIVATE_KEY_PATH}")"
  install -d -m 700 "${key_dir}" || exit 1
  work_dir="$(mktemp -d "${key_dir}/.bootstrap.XXXXXX")" || exit 1
  trap 'rm -rf "${work_dir}"' EXIT

  http_status="$(curl -sS -o "${work_dir}/response" -w '%{http_code}' \
    --connect-timeout 5 --max-time 20 --retry 3 --retry-connrefused \
    -H "X-Fleet-Bootstrap-Token: ${FLEET_BOOTSTRAP_TOKEN}" \
    "${API_URL}/api/v1/fleet/bootstrap/dispatcher-ssh-private-key")" || {
    log "WARNING: could not reach ${API_URL} for the storage SSH key (curl exit $?)."
    exit 1
  }

  if [ "${http_status}" != "200" ]; then
    case "${http_status}" in
      401) reason="FLEET_BOOTSTRAP_TOKEN is wrong or already used (it works once)" ;;
      403) reason="the API serves fleet bootstrap only to internal networks" ;;
      *) reason="unexpected status" ;;
    esac
    log "WARNING: ${API_URL} answered HTTP ${http_status} for the storage SSH key: ${reason}."
    exit 1
  fi

  # -j writes the key without adding a newline, so it lands byte-for-byte.
  if ! jq -j -e '.privateKey | select(type == "string" and length > 0)' \
    "${work_dir}/response" >"${work_dir}/key" 2>/dev/null; then
    log "WARNING: the storage SSH key response from ${API_URL} has no privateKey."
    exit 1
  fi

  if ! ssh-keygen -y -P '' -f "${work_dir}/key" >/dev/null 2>&1; then
    log "WARNING: the storage SSH key from ${API_URL} is not a valid private key; regenerate it."
    exit 1
  fi

  mv -f "${work_dir}/key" "${PRIVATE_KEY_PATH}" || exit 1
  log "installed the storage SSH key at ${PRIVATE_KEY_PATH}."
)

mkdir -p /var/run/sshd

if [ ! -f /etc/ssh/ssh_host_rsa_key ]; then
  ssh-keygen -A
fi

# Every long-running process is a background child of this script, and the container lives
# exactly as long as all of them: when one exits, the others get SIGTERM and the script exits with
# that child's status, so `restart: unless-stopped` restarts the dispatcher. (sshd used to be
# exec'd as the main process with GIT_SSH_ENABLED=true, and a dead dotnet left the container
# running without Smart HTTP.) docker stop's SIGTERM is passed on to every child; the script waits
# for them and exits 143. Background children ignore SIGINT in a non-interactive shell, so they
# always get SIGTERM.
CHILD_PIDS=()
CHILD_NAMES=()

start_child() {
  local name="$1"
  shift
  "$@" &
  CHILD_PIDS+=("$!")
  CHILD_NAMES+=("${name}")
}

child_name() {
  local i
  for i in "${!CHILD_PIDS[@]}"; do
    if [ "${CHILD_PIDS[i]}" = "$1" ]; then
      echo "${CHILD_NAMES[i]}"
      return
    fi
  done
  echo "pid $1"
}

stop_children() {
  kill -TERM "${CHILD_PIDS[@]}" 2>/dev/null || true
  wait "${CHILD_PIDS[@]}" 2>/dev/null || true
}

on_stop_signal() {
  log "received SIG$1; stopping ${CHILD_NAMES[*]}"
  stop_children
  exit "$2"
}
trap 'on_stop_signal TERM 143' TERM
trap 'on_stop_signal INT 130' INT

supervise_children() {
  local pid="" status=0
  wait -n -p pid "${CHILD_PIDS[@]}" || status=$?
  log "$(child_name "${pid}") exited with status ${status}; stopping the container"
  stop_children
  exit "${status}"
}

# Smart HTTP never reads the storage SSH key, so it starts first and keeps running when the fetch fails.
start_child dotnet dotnet /app/OpenGitBase.Dispatcher.dll --serve-http

# -s: an empty file left by an older entrypoint's failed fetch counts as missing.
if [ -n "${FLEET_BOOTSTRAP_TOKEN}" ] && [ ! -s "${PRIVATE_KEY_PATH}" ]; then
  if ! fetch_storage_ssh_key; then
    log "WARNING: running without a storage SSH key. Smart HTTP git works; SSH git cannot reach" \
      "storage until the key is provisioned (docs/deployment/fleet-dispatcher-ssh-key.md)."
  fi
fi

if [ "${GIT_SSH_ENABLED}" = "true" ]; then
  start_child sshd /usr/sbin/sshd -D -e
fi

supervise_children
