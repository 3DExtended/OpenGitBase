#!/usr/bin/env bash
set -euo pipefail

cd /app

SSH_ORIGINAL_COMMAND="${SSH_ORIGINAL_COMMAND:-}"
SSH_KEY_FINGERPRINT="${SSH_KEY_FINGERPRINT:-}"
SSH_PUBLIC_KEY="${SSH_PUBLIC_KEY:-}"
SSH_USER="${USER:-git}"

if [ -z "$SSH_KEY_FINGERPRINT" ]; then
    echo "SSH_KEY_FINGERPRINT not set (expected from authorized_keys environment= option)" >&2
    exit 1
fi

if [ -z "$SSH_PUBLIC_KEY" ]; then
    echo "SSH_PUBLIC_KEY not set (expected from authorized_keys environment= option)" >&2
    exit 1
fi

if [ -z "$SSH_ORIGINAL_COMMAND" ]; then
    echo "No SSH_ORIGINAL_COMMAND" >&2
    exit 1
fi

echo "dispatcher-wrapper: user=${SSH_USER} fingerprint=${SSH_KEY_FINGERPRINT} command=${SSH_ORIGINAL_COMMAND}" >&2

exec dotnet /app/OpenGitBase.Dispatcher.dll \
    "$SSH_ORIGINAL_COMMAND" \
    "$SSH_KEY_FINGERPRINT" \
    "$SSH_PUBLIC_KEY" \
    "$SSH_USER"
