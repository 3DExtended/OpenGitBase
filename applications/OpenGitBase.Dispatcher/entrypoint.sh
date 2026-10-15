#!/usr/bin/env bash
# Start Smart HTTP, fetch the dispatcher SSH private key from API fleet bootstrap, and start optional sshd.
set -euo pipefail

API_URL="${DISPATCHER_API_URL:-http://api:8080}"
FLEET_BOOTSTRAP_TOKEN="${FLEET_BOOTSTRAP_TOKEN:-}"
PRIVATE_KEY_PATH="${DISPATCHER_STORAGE_SSH_KEY_PATH:-${Dispatcher__StorageSshPrivateKeyPath:-/run/secrets/dispatcher_storage_ssh}}"
GIT_SSH_ENABLED="${GIT_SSH_ENABLED:-false}"
# ssh-auth-hook.sh and dispatcher-wrapper.sh source this file.
SESSION_ENV_FILE=/etc/opengitbase/dispatcher-session.env
# sshd runs SSH git sessions as this user; it must read the storage SSH key.
SESSION_USER=git

log() {
  echo "entrypoint: $*" >&2
}

# sshd starts AuthorizedKeysCommand and SSH sessions with a minimal environment, so the container's
# settings never reach ssh-auth-hook.sh, dispatcher-wrapper.sh or the .NET session. Save them for
# those scripts to source. Only DISPATCHER_API_URL and Dispatcher__* settings: no secrets, so the
# file is world-readable. The session reads the key from where this script installs it.
write_session_env() {
  local name
  install -d -m 755 "$(dirname "${SESSION_ENV_FILE}")"
  {
    printf 'export DISPATCHER_API_URL=%q\n' "${API_URL}"
    for name in $(compgen -e); do
      case "${name}" in
        Dispatcher__*) printf 'export %s=%q\n' "${name}" "${!name}" ;;
      esac
    done
    printf 'export Dispatcher__StorageSshPrivateKeyPath=%q\n' "${PRIVATE_KEY_PATH}"
  } >"${SESSION_ENV_FILE}.tmp"
  chmod 644 "${SESSION_ENV_FILE}.tmp"
  mv -f "${SESSION_ENV_FILE}.tmp" "${SESSION_ENV_FILE}"
}

# The session user may read the key but not replace it. Also applied to a key an older entrypoint
# left root-only.
secure_storage_ssh_key() {
  chown "root:${SESSION_USER}" "$(dirname "${PRIVATE_KEY_PATH}")" "${PRIVATE_KEY_PATH}"
  chmod 750 "$(dirname "${PRIVATE_KEY_PATH}")"
  chmod 640 "${PRIVATE_KEY_PATH}"
}

# Fetches the key into a temp dir next to PRIVATE_KEY_PATH and moves it into place only when the
# response parses and ssh-keygen accepts the key, so a failed fetch never leaves a file that the
# next start would take for a key. Runs in a subshell: errexit is off when called from `if`, so
# every step checks its own status, and the EXIT trap removes the temp dir.
fetch_storage_ssh_key() (
  umask 077
  key_dir="$(dirname "${PRIVATE_KEY_PATH}")"
  install -d -m 750 -g "${SESSION_USER}" "${key_dir}" || exit 1
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

  # Readable by the session user before it becomes visible at PRIVATE_KEY_PATH.
  chgrp "${SESSION_USER}" "${work_dir}/key" || exit 1
  chmod 640 "${work_dir}/key" || exit 1
  mv -f "${work_dir}/key" "${PRIVATE_KEY_PATH}" || exit 1
  log "installed the storage SSH key at ${PRIVATE_KEY_PATH}."
)

mkdir -p /var/run/sshd

if [ ! -f /etc/ssh/ssh_host_rsa_key ]; then
  ssh-keygen -A
fi

# Smart HTTP never reads the storage SSH key, so it starts first and keeps running when the fetch fails.
dotnet /app/OpenGitBase.Dispatcher.dll --serve-http &
DISPATCHER_HTTP_PID=$!

cleanup() {
  if kill -0 "${DISPATCHER_HTTP_PID}" 2>/dev/null; then
    kill "${DISPATCHER_HTTP_PID}" 2>/dev/null || true
  fi
}
trap cleanup EXIT

# -s: an empty file left by an older entrypoint's failed fetch counts as missing.
if [ -n "${FLEET_BOOTSTRAP_TOKEN}" ] && [ ! -s "${PRIVATE_KEY_PATH}" ]; then
  if ! fetch_storage_ssh_key; then
    log "WARNING: running without a storage SSH key. Smart HTTP git works; SSH git cannot reach" \
      "storage until the key is provisioned (docs/deployment/fleet-dispatcher-ssh-key.md)."
  fi
fi

if [ -s "${PRIVATE_KEY_PATH}" ]; then
  secure_storage_ssh_key
fi

write_session_env

if [ "${GIT_SSH_ENABLED}" = "true" ]; then
  exec /usr/sbin/sshd -D -e
fi

wait "${DISPATCHER_HTTP_PID}"
