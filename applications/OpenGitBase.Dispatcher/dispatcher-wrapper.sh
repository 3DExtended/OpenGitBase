#!/usr/bin/env bash
set -euo pipefail

cd /app

echo "dispatcher-wrapper.sh: executing..."

SSH_ORIGINAL_COMMAND="${SSH_ORIGINAL_COMMAND:-}"
SSH_KEY_FINGERPRINT="${SSH_KEY_FINGERPRINT:-}"
SSH_PUBLIC_KEY="${SSH_PUBLIC_KEY:-}"
SSH_USER="${USER:-git}"
API_URL="${DISPATCHER_API_URL:-http://api:8080}"

if [ -z "$SSH_KEY_FINGERPRINT" ]; then
    echo "dispatcher-wrapper.sh: SSH_KEY_FINGERPRINT not set (expected from authorized_keys environment= option)" >&2
    exit 1
fi

if [ -z "$SSH_PUBLIC_KEY" ]; then
    RESPONSE_FILE="$(mktemp)"
    trap 'rm -f "$RESPONSE_FILE"' EXIT
    HTTP_CODE=$(curl -sS -o "$RESPONSE_FILE" -w "%{http_code}" --get \
        "${API_URL}/api/v1/ssh-authentication/by-fingerprint" \
        --data-urlencode "fingerprint=${SSH_KEY_FINGERPRINT}") || {
        echo "dispatcher-wrapper: API request failed" >&2
        exit 1
    }
    if [ "$HTTP_CODE" != "200" ]; then
        echo "dispatcher-wrapper: public key lookup failed (HTTP ${HTTP_CODE})" >&2
        exit 1
    fi
    SSH_PUBLIC_KEY="$(jq -er '.publicSshKey' "$RESPONSE_FILE")" || {
        echo "dispatcher-wrapper: invalid API response" >&2
        exit 1
    }
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
