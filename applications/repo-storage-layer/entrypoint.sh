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
mkdir -p /home/git/.ssh /srv/git
chown -R git:git /home/git /srv/git
chmod 700 /home/git/.ssh
if [ -f /home/git/.ssh/authorized_keys ]; then
  chmod 600 /home/git/.ssh/authorized_keys
  chown git:git /home/git/.ssh/authorized_keys
fi

# SSH Daemon im Vordergrund starten
exec /usr/sbin/sshd -D -e