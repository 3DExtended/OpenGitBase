#!/usr/bin/env bash
# Copyright (c) 2026 OpenGitBase Authors
# SPDX-License-Identifier: LicenseRef-OpenGitBase-1.0
# See LICENSE at repository root.

set -euo pipefail

STORAGE_STANDALONE="${STORAGE_STANDALONE:-0}"

mkdir -p /var/run/sshd

# SSH Hostkeys initialisieren
if [ ! -f /etc/ssh/ssh_host_rsa_key ]; then
  ssh-keygen -A
fi

# git user setup
mkdir -p /home/git/.ssh /srv/git /var/lib/opengitbase/artifacts /var/lib/opengitbase/watermarks /var/lib/opengitbase/repo-roles
chown -R git:git /home/git /srv/git
chmod 700 /home/git/.ssh
if [ -f /home/git/.ssh/authorized_keys ]; then
  chmod 600 /home/git/.ssh/authorized_keys
  chown git:git /home/git/.ssh/authorized_keys
fi

source /usr/local/bin/storage-agent.sh

if [ "${STORAGE_STANDALONE}" = "1" ]; then
  if [ -z "${STORAGE_API_TOKEN:-}" ] && [ ! -f "${TOKEN_FILE}" ]; then
    echo "entrypoint: STORAGE_API_TOKEN is required in standalone mode" >&2
    exit 1
  fi
  if [ -f "${TOKEN_FILE}" ]; then
    chown git:git "${TOKEN_FILE}" 2>/dev/null || true
  fi
  chown -R git:git /var/lib/opengitbase 2>/dev/null || true
else
  configure_dispatcher_authorized_keys

  register_node
  if [ -f "${TOKEN_FILE}" ]; then
    export STORAGE_API_TOKEN="$(cat "${TOKEN_FILE}")"
  fi
  chown -R git:git /var/lib/opengitbase
  if [ -n "${STORAGE_API_URL:-}" ] && [ -f "${TOKEN_FILE}" ]; then
    printf '%s' "${STORAGE_API_URL}" > /var/lib/opengitbase/api-url
    printf '%s' "${NODE_ID}" > /var/lib/opengitbase/node-id
    chown git:git /var/lib/opengitbase/api-url /var/lib/opengitbase/node-id
  fi
fi

python3 /usr/local/bin/storage-http-server.py &
STORAGE_HTTP_PID=$!

/usr/local/bin/storage-git-http.sh &
STORAGE_GIT_HTTP_PID=$!

if [ "${STORAGE_STANDALONE}" != "1" ]; then
  start_storage_agent_background
fi

cleanup() {
  for pid in "$STORAGE_HTTP_PID" "$STORAGE_GIT_HTTP_PID"; do
    if kill -0 "$pid" 2>/dev/null; then
      kill "$pid" 2>/dev/null || true
    fi
  done
}
trap cleanup EXIT

if [ "${STORAGE_STANDALONE}" = "1" ]; then
  wait "$STORAGE_HTTP_PID" "$STORAGE_GIT_HTTP_PID"
else
  # SSH Daemon im Vordergrund starten
  exec /usr/sbin/sshd -D -e
fi
