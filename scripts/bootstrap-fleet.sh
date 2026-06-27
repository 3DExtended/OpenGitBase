#!/usr/bin/env bash
# Bootstrap fleet SSH keys and storage node enrollments via admin API.
set -euo pipefail

API_URL="${API_URL:-http://localhost:8089}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
PKI_SCRIPT="${REPO_ROOT}/docker/pki/generate-node-pki.sh"
OVERRIDE_FILE="${OVERRIDE_FILE:-${REPO_ROOT}/docker-compose.override.yml}"
EXAMPLE_FILE="${EXAMPLE_FILE:-${REPO_ROOT}/docker-compose.override.example.yml}"

if [ -x "${PKI_SCRIPT}" ]; then
  echo "==> Generating storage node certificates"
  "${PKI_SCRIPT}"
fi

ADMIN_USER="${ADMIN_USER:-admin}"
ADMIN_PASS="${ADMIN_PASS:-change-me-admin}"

echo "==> Checking API health at ${API_URL}"
if ! curl -fsS "${API_URL}/health" >/dev/null; then
  echo "API is not reachable at ${API_URL}." >&2
  echo "Start postgres, API replicas, and HAProxy first:" >&2
  echo "  docker compose up -d --build postgres api-1 api-2 ssh-lb" >&2
  echo "  curl -fsS ${API_URL}/health" >&2
  exit 1
fi

login() {
  local response http_code
  response=$(curl -sS -w "\n%{http_code}" -X POST "${API_URL}/signin/login" \
    -H "Content-Type: application/json" \
    -d "{\"username\":\"${ADMIN_USER}\",\"password\":\"${ADMIN_PASS}\"}")
  http_code="${response##*$'\n'}"
  response="${response%$'\n'*}"
  if [ "${http_code}" != "200" ]; then
    echo "Admin login failed (HTTP ${http_code})." >&2
    echo "Username: ${ADMIN_USER}" >&2
    echo "The default password is change-me-admin (from AdminSeed in appsettings)." >&2
    echo "If the admin user already exists, AdminUserSeedService does not reset the password." >&2
    echo "Fix options:" >&2
    echo "  - Set ADMIN_PASS to the current admin password, or" >&2
    echo "  - Wipe the database and re-seed: docker compose down -v && docker compose up -d --build postgres api-1 api-2 ssh-lb" >&2
    [ -n "${response}" ] && echo "Response: ${response}" >&2
    exit 1
  fi
  printf '%s' "${response}"
}

echo "==> Signing in as admin"
TOKEN=$(login | tr -d '\n\r"')

echo "==> Generating dispatcher SSH keys"
FLEET_JSON=$(curl -fsS -X POST "${API_URL}/admin/fleet/dispatcher-ssh-keys/generate" \
  -H "Authorization: Bearer ${TOKEN}")
FLEET_BOOTSTRAP_TOKEN=$(echo "${FLEET_JSON}" | python3 -c 'import json,sys; print(json.load(sys.stdin)["fleetBootstrapToken"])')
DISPATCHER_SSH_PUBLIC_KEY=$(echo "${FLEET_JSON}" | python3 -c 'import json,sys; print(json.load(sys.stdin)["dispatcherSshPublicKey"])')

create_enrollment() {
  local node_id="$1"
  curl -fsS -X POST "${API_URL}/admin/storage-enrollments" \
    -H "Authorization: Bearer ${TOKEN}" \
    -H "Content-Type: application/json" \
    -d "{\"nodeId\":\"${node_id}\"}" \
    | python3 -c 'import json,sys; print(json.load(sys.stdin)["enrollmentToken"])'
}

echo "==> Creating storage enrollments"
STORAGE_1_TOKEN=$(create_enrollment storage-1)
STORAGE_2_TOKEN=$(create_enrollment storage-2)
STORAGE_3_TOKEN=$(create_enrollment storage-3)

if [ ! -f "${OVERRIDE_FILE}" ]; then
  if [ ! -f "${EXAMPLE_FILE}" ]; then
    echo "Missing ${EXAMPLE_FILE}" >&2
    exit 1
  fi
  cp "${EXAMPLE_FILE}" "${OVERRIDE_FILE}"
  echo "Created ${OVERRIDE_FILE} from example — set REPLACE_WITH_CLOUDFLARE_TUNNEL_TOKEN before starting the tunnel"
fi

python3 - "${OVERRIDE_FILE}" "${EXAMPLE_FILE}" "${STORAGE_1_TOKEN}" "${STORAGE_2_TOKEN}" "${STORAGE_3_TOKEN}" "${FLEET_BOOTSTRAP_TOKEN}" "${DISPATCHER_SSH_PUBLIC_KEY}" <<'PY'
import json
import re
import sys
from pathlib import Path

override_path = Path(sys.argv[1])
example_path = Path(sys.argv[2])
storage_1 = sys.argv[3]
storage_2 = sys.argv[4]
storage_3 = sys.argv[5]
fleet_token = sys.argv[6]
dispatcher_key = sys.argv[7]


def q(value: str) -> str:
    return json.dumps(value)


def patch_service_env(content: str, service: str, updates: dict[str, str]) -> str:
    lines = content.splitlines(keepends=True)
    i = 0
    while i < len(lines):
        if lines[i].rstrip() == f"  {service}:":
            i += 1
            while i < len(lines) and lines[i].startswith("    "):
                stripped = lines[i].strip()
                for key, value in updates.items():
                    if stripped.startswith(f"{key}:"):
                        indent = lines[i][: len(lines[i]) - len(lines[i].lstrip())]
                        lines[i] = f"{indent}{key}: {q(value)}\n"
                i += 1
            break
        i += 1
    return "".join(lines)


def patch_cloudflare_token(content: str, token: str) -> str:
    return re.sub(
        r"(--token )\S+",
        rf"\1{token}",
        content,
        count=1,
    )


content = override_path.read_text()

cloudflare_token = "REPLACE_WITH_CLOUDFLARE_TUNNEL_TOKEN"
legacy_env = override_path.parent / "docker" / ".env"
if legacy_env.exists():
    for line in legacy_env.read_text().splitlines():
        if line.startswith("CLOUDFLARE_TUNNEL_KEY="):
            value = line.split("=", 1)[1].strip().strip('"')
            if value:
                cloudflare_token = value
match = re.search(r"--token (\S+)", content)
if match and match.group(1) != "REPLACE_WITH_CLOUDFLARE_TUNNEL_TOKEN":
    cloudflare_token = match.group(1)

content = patch_service_env(
    content,
    "storage-1",
    {
        "STORAGE_ENROLLMENT_TOKEN": storage_1,
        "DISPATCHER_SSH_PUBLIC_KEY": dispatcher_key,
    },
)
content = patch_service_env(
    content,
    "storage-2",
    {
        "STORAGE_ENROLLMENT_TOKEN": storage_2,
        "DISPATCHER_SSH_PUBLIC_KEY": dispatcher_key,
    },
)
content = patch_service_env(
    content,
    "storage-3",
    {
        "STORAGE_ENROLLMENT_TOKEN": storage_3,
        "DISPATCHER_SSH_PUBLIC_KEY": dispatcher_key,
    },
)
content = patch_service_env(
    content,
    "dispatcher-1",
    {"FLEET_BOOTSTRAP_TOKEN": fleet_token},
)
content = patch_service_env(
    content,
    "dispatcher-2",
    {"FLEET_BOOTSTRAP_TOKEN": fleet_token},
)
content = patch_cloudflare_token(content, cloudflare_token)

header = f"""# Generated by scripts/bootstrap-fleet.sh — do not commit
# Template: docker-compose.override.example.yml

"""
if not content.startswith("# Generated by scripts/bootstrap-fleet.sh"):
    content = header + content.lstrip()

override_path.write_text(content)
print(f"Wrote {override_path}")
PY

echo "Start the stack with:"
echo "  docker compose -f docker-compose.yml -f docker-compose.override.yml up -d --build"
echo "  # Production (Unraid) with Cloudflare tunnel:"
echo "  source scripts/docker-env.production.sh && docker compose -f docker-compose.yml -f docker-compose.override.yml up -d --build"
