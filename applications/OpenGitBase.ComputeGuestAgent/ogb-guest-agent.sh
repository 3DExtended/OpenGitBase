#!/bin/sh
set -eu

PORT="${OGB_GUEST_AGENT_VSOCK_PORT:-5000}"
MARKER="/usr/local/lib/opengitbase/guest-agent-ready"
PROJECT_DIR="${CI_PROJECT_DIR:-/workspace/repo}"

mkdir -p "$(dirname "${MARKER}")"
: > "${MARKER}"

if ! command -v socat >/dev/null 2>&1; then
  echo "ogb-guest-agent: socat is required" >&2
  exit 1
fi

if [ "${OGB_VIRTIOFS:-0}" = "1" ]; then
  mkdir -p "${PROJECT_DIR}"
  if ! mountpoint -q "${PROJECT_DIR}" 2>/dev/null; then
    mount -t virtiofs workspace "${PROJECT_DIR}" || echo "ogb-guest-agent: virtiofs mount failed" >&2
  fi
fi

echo "ogb-guest-agent: listening on vsock port ${PORT}" >&2

while true; do
  socat - "VSOCK-LISTEN:${PORT},fork" | while IFS= read -r request; do
    [ -z "${request}" ] && continue
    user="$(printf '%s' "${request}" | sed -n 's/.*"user"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')"
    cwd="$(printf '%s' "${request}" | sed -n 's/.*"cwd"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')"
    script="$(printf '%s' "${request}" | sed -n 's/.*"script"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')"
    [ -z "${user}" ] && user="ogb"
    [ -z "${cwd}" ] && cwd="${PROJECT_DIR}"
    [ -z "${script}" ] && script="true"
    if [ "${user}" = "root" ]; then
      sh -c "cd '${cwd}' && ${script}" 2>&1 | while IFS= read -r line; do
        printf '{"stream":"stdout","line":"%s"}\n' "$(printf '%s' "${line}" | sed 's/"/\\"/g')"
      done
    else
      su -s /bin/sh "${user}" -c "cd '${cwd}' && ${script}" 2>&1 | while IFS= read -r line; do
        printf '{"stream":"stdout","line":"%s"}\n' "$(printf '%s' "${line}" | sed 's/"/\\"/g')"
      done
    fi
    exit_code=$?
    printf '{"exitCode":%s}\n' "${exit_code}"
  done
done
