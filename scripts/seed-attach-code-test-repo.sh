#!/usr/bin/env bash
# Create a public repo with sample files for testing discussion "attach to code".
set -euo pipefail

API_URL="${API_URL:-http://localhost:8089}"
GIT_HTTP_PORT="${GIT_HTTP_PORT:-8089}"
TEST_USER="${TEST_USER:-attach-code-dev}"
TEST_PASS="${TEST_PASS:-AttachCode123!}"
TEST_REPO="${TEST_REPO:-attach-demo}"
WORK_DIR="$(mktemp -d)"

cleanup() {
  rm -rf "${WORK_DIR}"
}
trap cleanup EXIT

json_field() {
  python3 -c 'import json,sys; d=json.load(open(sys.argv[1])); print(d[sys.argv[2]])' "$1" "$2"
}

echo "==> Seeding attach-to-code test repo via ${API_URL}"

register_status="$(curl -sS -o "${WORK_DIR}/register.json" -w '%{http_code}' \
  -X POST "${API_URL}/api/register/register" \
  -H "Content-Type: application/json" \
  -d "{\"username\":\"${TEST_USER}\",\"email\":\"${TEST_USER}@example.com\",\"password\":\"${TEST_PASS}\"}")"
echo "    register: ${register_status}"

JWT="$(curl -fsS -X POST "${API_URL}/api/signin/login" \
  -H "Content-Type: application/json" \
  -d "{\"username\":\"${TEST_USER}\",\"password\":\"${TEST_PASS}\"}" \
  | python3 -c 'import json,sys; raw=sys.stdin.read().strip(); print(json.loads(raw) if raw.startswith("\"") or raw.startswith("{") else raw)')"

curl -fsS -X POST "${API_URL}/api/account/debug/verify-email" \
  -H "Authorization: Bearer ${JWT}" >/dev/null 2>&1 || true

create_status="$(curl -sS -o "${WORK_DIR}/repo.json" -w '%{http_code}' \
  -X POST "${API_URL}/api/repository/${TEST_REPO}" \
  -H "Authorization: Bearer ${JWT}" \
  -H "Content-Type: application/json" \
  -d '{"repositoryName":"Attach Code Demo","isPrivate":false}')"
if [[ "${create_status}" != "201" && "${create_status}" != "409" ]]; then
  echo "FAIL: create repo expected 201 or 409, got ${create_status}" >&2
  cat "${WORK_DIR}/repo.json" >&2
  exit 1
fi
echo "    create repo: ${create_status}"

echo "==> Waiting for storage provisioning"
sleep 10

WRITE_PAT="$(curl -fsS -X POST "${API_URL}/api/git-access-token" \
  -H "Authorization: Bearer ${JWT}" \
  -H "Content-Type: application/json" \
  -d '{"name":"attach-code-seed","scope":"write"}' \
  | python3 -c 'import json,sys; print(json.load(sys.stdin)["token"])')"

REMOTE="http://git:${WRITE_PAT}@127.0.0.1:${GIT_HTTP_PORT}/${TEST_USER}/${TEST_REPO}.git"

mkdir -p "${WORK_DIR}/repo/src/utils" "${WORK_DIR}/repo/lib"
cat > "${WORK_DIR}/repo/README.md" <<'EOF'
# Attach Code Demo

Sample repository for testing discussion code anchors.
EOF

cat > "${WORK_DIR}/repo/src/main.ts" <<'EOF'
import { formatLabel } from './utils/helper'

export function greet(name: string): string {
  return formatLabel(`Hello, ${name}!`)
}

if (import.meta.main) {
  console.log(greet('OpenGitBase'))
}
EOF

cat > "${WORK_DIR}/repo/src/utils/helper.ts" <<'EOF'
export function formatLabel(text: string): string {
  return text.trim()
}

export function pickLines(source: string, start: number, end: number): string {
  return source.split('\n').slice(start - 1, end).join('\n')
}
EOF

cat > "${WORK_DIR}/repo/lib/config.json" <<'EOF'
{
  "appName": "attach-demo",
  "features": {
    "discussions": true
  }
}
EOF

git -C "${WORK_DIR}/repo" init -b main >/dev/null
git -C "${WORK_DIR}/repo" config user.email "${TEST_USER}@example.com"
git -C "${WORK_DIR}/repo" config user.name "${TEST_USER}"
git -C "${WORK_DIR}/repo" add .
git -C "${WORK_DIR}/repo" commit -m "Add sample files for attach-to-code testing" >/dev/null
git -C "${WORK_DIR}/repo" remote add origin "${REMOTE}"
git -C "${WORK_DIR}/repo" push -u origin main --force

refs_status="$(curl -sS -o "${WORK_DIR}/refs.json" -w '%{http_code}' \
  "${API_URL}/api/repository/by-slug/${TEST_USER}/${TEST_REPO}/content/refs")"
is_empty="$(python3 -c 'import json; print(str(json.load(open("'"${WORK_DIR}/refs.json"'")).get("isEmpty", True)).lower())')"

if [[ "${refs_status}" != "200" || "${is_empty}" == "true" ]]; then
  echo "FAIL: repository refs not ready (status=${refs_status}, isEmpty=${is_empty})" >&2
  exit 1
fi

default_ref="$(python3 -c 'import json; print(json.load(open("'"${WORK_DIR}/refs.json"'")).get("defaultRef") or "")')"
tree_status="$(curl -sS -o "${WORK_DIR}/tree.json" -w '%{http_code}' \
  "${API_URL}/api/repository/by-slug/${TEST_USER}/${TEST_REPO}/content/tree?refName=${default_ref}&path=")"

echo
echo "Attach-to-code test repo ready:"
echo "  Owner:    ${TEST_USER}"
echo "  Repo:     ${TEST_REPO}"
echo "  Password: ${TEST_PASS}"
echo "  Web:      http://localhost:3001/${TEST_USER}/${TEST_REPO}"
echo "  Blob:     http://localhost:3001/${TEST_USER}/${TEST_REPO}/blob/main/src/main.ts"
echo "  Discussions: http://localhost:3001/${TEST_USER}/${TEST_REPO}/discussions"
echo "  defaultRef: ${default_ref}"
echo "  tree API: ${tree_status}"
