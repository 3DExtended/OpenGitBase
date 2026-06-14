#!/usr/bin/env bash
set -euo pipefail

SSH_USER="${1:-}"
SSH_KEY_FP="${2:-}"
SSH_KEY_TYPE="${3:-}"
SSH_KEY_B64="${4:-}"

echo "ssh-auth-hook executing... user=${SSH_USER} fingerprint=${SSH_KEY_FP}" >&2

if [ -z "$SSH_USER" ]; then
    echo "No SSH_USER" >&2
    exit 1
fi

if [ -z "$SSH_KEY_FP" ]; then
    echo "No key fingerprint" >&2
    exit 1
fi

API_URL="${DISPATCHER_API_URL:-http://api:8080}"
RESPONSE_FILE="$(mktemp)"
trap 'rm -f "$RESPONSE_FILE"' EXIT

HTTP_CODE=$(curl -sS -o "$RESPONSE_FILE" -w "%{http_code}" --get \
    "${API_URL}/api/v1/ssh-authentication/by-fingerprint" \
    --data-urlencode "fingerprint=${SSH_KEY_FP}") || {
    echo "ssh-auth-hook: API request failed" >&2
    exit 1
}

if [ "$HTTP_CODE" != "200" ]; then
    echo "ssh-auth-hook: authentication denied (HTTP ${HTTP_CODE})" >&2
    exit 1
fi

AUTHORIZED_KEYS_LINE="$(jq -er '.authorizedKeysLine' "$RESPONSE_FILE")" || {
    echo "ssh-auth-hook: invalid API response" >&2
    exit 1
}

echo "$AUTHORIZED_KEYS_LINE"
exit 0
