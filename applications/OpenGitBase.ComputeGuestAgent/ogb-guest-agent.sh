#!/bin/sh
set -eu

PORT="${OGB_GUEST_AGENT_VSOCK_PORT:-5000}"
MARKER="/usr/local/lib/opengitbase/guest-agent-ready"

mkdir -p "$(dirname "${MARKER}")"
: > "${MARKER}"

if ! command -v socat >/dev/null 2>&1; then
  echo "ogb-guest-agent: socat is required" >&2
  exit 1
fi

echo "ogb-guest-agent: listening on vsock port ${PORT}" >&2

while true; do
  socat - "VSOCK-LISTEN:${PORT},fork" | while IFS= read -r request; do
    [ -z "${request}" ] && continue
    user="$(printf '%s' "${request}" | sed -n 's/.*"user"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')"
    cwd="$(printf '%s' "${request}" | sed -n 's/.*"cwd"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')"
    script="$(printf '%s' "${request}" | sed -n 's/.*"script"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')"
    [ -z "${user}" ] && user="ogb"
    [ -z "${cwd}" ] && cwd="/workspace/repo"
    [ -z "${script}" ] && script="true"
    if [ "${user}" = "root" ]; then
      sh -c "cd '${cwd}' && ${script}"
    else
      su -s /bin/sh "${user}" -c "cd '${cwd}' && ${script}"
    fi
    exit_code=$?
    printf '{"exitCode":%s}\n' "${exit_code}"
  done
done
