#!/usr/bin/env bash
set -euo pipefail

# Compose-only seed for minimal dev egress allowlist entries.
API_URL="${API_URL:-http://localhost:8089}"
ADMIN_USER="${ADMIN_USER:-admin}"
ADMIN_PASS="${ADMIN_PASS:-change-me-admin}"

login() {
  curl -fsS -X POST "${API_URL}/api/signin/login" \
    -H "Content-Type: application/json" \
    -d "{\"username\":\"${ADMIN_USER}\",\"password\":\"${ADMIN_PASS}\"}" \
    | tr -d '\n\r"'
}

TOKEN="$(login)"

for domain in registry.npmjs.org github.com; do
  echo "==> Seeding platform allowlist entry for ${domain}"
  curl -fsS -X POST "${API_URL}/pipeline/egress/domain-requests" \
    -H "Authorization: Bearer ${TOKEN}" \
    -H "Content-Type: application/json" \
    -d "{\"domain\":\"${domain}\",\"justification\":\"Compose dev seed\",\"scope\":0}" >/dev/null || true
done

echo "Compose egress allowlist seed complete. Approve pending requests in admin UI if required."
