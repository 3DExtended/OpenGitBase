#!/usr/bin/env bash
# Copyright (c) 2026 OpenGitBase Authors
# SPDX-License-Identifier: LicenseRef-OpenGitBase-1.0
# See LICENSE at repository root.

set -euo pipefail

STORAGE_STANDALONE="${STORAGE_STANDALONE:-0}"

log() {
  echo "entrypoint: $*" >&2
}

# Every long-running process is a background child of this script, and the container lives
# exactly as long as all of them: when one exits, the others get SIGTERM and the script exits with
# that child's status, so `restart: unless-stopped` restarts the whole node. (sshd used to be
# exec'd as the main process, and a dead storage-http-server.py or nginx left the container
# running and unhealthy.) docker stop's SIGTERM is passed on to every child; the script waits for
# them and exits 143. Background children ignore SIGINT in a non-interactive shell, so they always
# get SIGTERM.
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

start_child storage-http-server.py python3 /usr/local/bin/storage-http-server.py
start_child nginx /usr/local/bin/storage-git-http.sh

if [ "${STORAGE_STANDALONE}" != "1" ]; then
  start_child storage-agent heartbeat_loop
  start_child sshd /usr/sbin/sshd -D -e
fi

supervise_children
