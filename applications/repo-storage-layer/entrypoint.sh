#!/usr/bin/env bash
# Copyright (c) 2026 OpenGitBase Authors
# SPDX-License-Identifier: LicenseRef-OpenGitBase-1.0
# See LICENSE at repository root.

set -euo pipefail

mkdir -p /var/run/sshd

# SSH Hostkeys initialisieren
if [ ! -f /etc/ssh/ssh_host_rsa_key ]; then
  ssh-keygen -A
fi

# git user setup
mkdir -p /home/git/.ssh /srv/git /var/lib/opengitbase
chown -R git:git /home/git /srv/git
chmod 700 /home/git/.ssh
if [ -f /home/git/.ssh/authorized_keys ]; then
  chmod 600 /home/git/.ssh/authorized_keys
  chown git:git /home/git/.ssh/authorized_keys
fi

source /usr/local/bin/storage-agent.sh

configure_dispatcher_authorized_keys

python3 /usr/local/bin/storage-http-server.py &
STORAGE_HTTP_PID=$!

start_storage_agent_background

cleanup() {
  if kill -0 "$STORAGE_HTTP_PID" 2>/dev/null; then
    kill "$STORAGE_HTTP_PID" 2>/dev/null || true
  fi
}
trap cleanup EXIT

# SSH Daemon im Vordergrund starten
exec /usr/sbin/sshd -D -e
