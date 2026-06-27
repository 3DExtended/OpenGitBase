#!/usr/bin/env bash
# Restore correct Cloudflare tunnel ingress for opengitbase.com production.
# Remote config version 7 had www->:8081 and apex->http://opengitbase.com (loop).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
OVERRIDE_FILE="${OVERRIDE_FILE:-${REPO_ROOT}/docker-compose.override.yml}"

ACCOUNT_ID="${CLOUDFLARE_ACCOUNT_ID:-}"
TUNNEL_ID="${CLOUDFLARE_TUNNEL_ID:-}"

read_tunnel_token_from_override() {
  if [ ! -f "${OVERRIDE_FILE}" ]; then
    return 1
  fi
  python3 - "${OVERRIDE_FILE}" <<'PY'
import re
import sys
from pathlib import Path

text = Path(sys.argv[1]).read_text()
match = re.search(r"--token\s+(\S+)", text)
if match:
    print(match.group(1))
PY
}

if [ -z "${CLOUDFLARE_API_TOKEN:-}" ]; then
  CLOUDFLARE_API_TOKEN="$(read_tunnel_token_from_override || true)"
fi

if [ -z "${CLOUDFLARE_API_TOKEN:-}" ]; then
  echo "Set CLOUDFLARE_API_TOKEN or add --token to ${OVERRIDE_FILE}." >&2
  exit 1
fi

if [ -z "${ACCOUNT_ID}" ] || [ -z "${TUNNEL_ID}" ]; then
  read -r decoded_account decoded_tunnel < <(
    python3 -c "import base64,json,sys; t=sys.argv[1]; p=json.loads(base64.urlsafe_b64decode(t+'==')); print(p.get('a',''), p.get('t',''))" "${CLOUDFLARE_API_TOKEN}"
  )
  ACCOUNT_ID="${ACCOUNT_ID:-${decoded_account}}"
  TUNNEL_ID="${TUNNEL_ID:-${decoded_tunnel}}"
fi

payload="$(cat <<'EOF'
{
  "config": {
    "ingress": [
      {
        "hostname": "git.opengitbase.com",
        "service": "ssh://ssh-lb:22",
        "originRequest": {}
      },
      {
        "hostname": "www.opengitbase.com",
        "service": "http://ssh-lb:8080",
        "originRequest": {}
      },
      {
        "hostname": "api.opengitbase.com",
        "service": "http://api-lb:8080",
        "originRequest": {}
      },
      {
        "hostname": "opengitbase.com",
        "service": "http://ssh-lb:8080",
        "originRequest": {}
      },
      {
        "service": "http_status:404"
      }
    ]
  }
}
EOF
)"

echo "==> Updating tunnel ${TUNNEL_ID} ingress in account ${ACCOUNT_ID}"
response="$(curl -fsS \
  -X PUT \
  "https://api.cloudflare.com/client/v4/accounts/${ACCOUNT_ID}/cfd_tunnel/${TUNNEL_ID}/configurations" \
  -H "Authorization: Bearer ${CLOUDFLARE_API_TOKEN}" \
  -H "Content-Type: application/json" \
  --data "${payload}")"

python3 -c 'import json,sys; r=json.load(sys.stdin); print("success:", r.get("success")); print(json.dumps(r.get("result",{}), indent=2) if r.get("success") else r)' <<<"${response}"

echo "==> Restarting tunnel container"
cd "${REPO_ROOT}"
COMPOSE_PROFILES=production-tunnel docker compose -f docker-compose.yml -f docker-compose.override.yml restart opengitbase_cloudflare_tunnel

echo "Done. Verify: curl -fsS https://www.opengitbase.com/ >/dev/null && echo OK"
