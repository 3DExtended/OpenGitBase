#!/usr/bin/env bash
# Create a public repo with sample files and seeded discussion sub-threads.
set -euo pipefail

API_URL="${API_URL:-http://localhost:8089}"
API_BASE="${API_BASE:-${API_URL}/api}"
GIT_HTTP_PORT="${GIT_HTTP_PORT:-8089}"
TEST_USER="${TEST_USER:-subthread-dev}"
TEST_PASS="${TEST_PASS:-SubThread123!}"
TEST_REPO="${TEST_REPO:-subthread-demo}"
WORK_DIR="$(mktemp -d)"

cleanup() {
  rm -rf "${WORK_DIR}"
}
trap cleanup EXIT

json_field() {
  python3 -c "import json; d=json.load(open('${1}')); print(${2})"
}

echo "==> Seeding discussion sub-thread test repo via ${API_URL}"

curl -sS -o /dev/null \
  -X POST "${API_BASE}/register/register" \
  -H "Content-Type: application/json" \
  -d "{\"username\":\"${TEST_USER}\",\"email\":\"${TEST_USER}@example.com\",\"password\":\"${TEST_PASS}\"}" \
  2>/dev/null || true

JWT="$(curl -fsS -X POST "${API_BASE}/signin/login" \
  -H "Content-Type: application/json" \
  -d "{\"username\":\"${TEST_USER}\",\"password\":\"${TEST_PASS}\"}" \
  | python3 -c 'import json,sys; raw=sys.stdin.read().strip(); print(json.loads(raw) if raw.startswith("\"") or raw.startswith("{") else raw)')"

curl -fsS -X POST "${API_BASE}/account/debug/verify-email" \
  -H "Authorization: Bearer ${JWT}" >/dev/null 2>&1 || true

create_status="$(curl -sS -o "${WORK_DIR}/repo.json" -w '%{http_code}' \
  -X POST "${API_BASE}/repository/${TEST_REPO}" \
  -H "Authorization: Bearer ${JWT}" \
  -H "Content-Type: application/json" \
  -d '{"repositoryName":"Sub-thread Demo","isPrivate":false}')"
if [[ "${create_status}" != "201" && "${create_status}" != "409" ]]; then
  echo "FAIL: create repo expected 201 or 409, got ${create_status}" >&2
  cat "${WORK_DIR}/repo.json" >&2
  exit 1
fi
echo "    create repo: ${create_status}"

echo "==> Waiting for storage provisioning"
sleep 10

WRITE_PAT="$(curl -fsS -X POST "${API_BASE}/git-access-token" \
  -H "Authorization: Bearer ${JWT}" \
  -H "Content-Type: application/json" \
  -d '{"name":"subthread-seed","scope":"write"}' \
  | python3 -c 'import json,sys; print(json.load(sys.stdin)["token"])')"

REMOTE="http://git:${WRITE_PAT}@127.0.0.1:${GIT_HTTP_PORT}/${TEST_USER}/${TEST_REPO}.git"

mkdir -p "${WORK_DIR}/repo/src/discussions"
cat > "${WORK_DIR}/repo/README.md" <<'EOF'
# Sub-thread Demo

Repository for testing discussion replies and per-comment resolution.
EOF

cat > "${WORK_DIR}/repo/src/discussions/service.ts" <<'EOF'
export function threadKey(id: string): string {
  return `thread:${id}`
}

export function isResolved(resolvedAt: string | null): boolean {
  return resolvedAt !== null
}
EOF

cat > "${WORK_DIR}/repo/src/main.ts" <<'EOF'
import { isResolved } from './discussions/service'

export function statusLabel(resolvedAt: string | null): string {
  return isResolved(resolvedAt) ? 'resolved' : 'open'
}
EOF

git -C "${WORK_DIR}/repo" init -b main >/dev/null
git -C "${WORK_DIR}/repo" config user.email "${TEST_USER}@example.com"
git -C "${WORK_DIR}/repo" config user.name "${TEST_USER}"
git -C "${WORK_DIR}/repo" add .
git -C "${WORK_DIR}/repo" commit -m "Add sample files for sub-thread testing" >/dev/null
git -C "${WORK_DIR}/repo" remote add origin "${REMOTE}"
git -C "${WORK_DIR}/repo" push -u origin main --force

refs_json="$(curl -fsS "${API_BASE}/repository/by-slug/${TEST_USER}/${TEST_REPO}/content/refs")"
default_ref="$(python3 -c "import json,sys; print(json.load(sys.stdin).get('defaultRef') or 'main')" <<< "${refs_json}")"
commit_sha="$(python3 -c "
import json,sys
data=json.load(sys.stdin)
for b in data.get('branches', []):
    if b.get('name') == '${default_ref}':
        print(b.get('commitSha',''))
        break
" <<< "${refs_json}")"

DISC_PATH="${API_BASE}/repository/by-slug/${TEST_USER}/${TEST_REPO}/discussions"

disc_status="$(curl -sS -o "${WORK_DIR}/disc.json" -w '%{http_code}' \
  -X POST "${DISC_PATH}" \
  -H "Authorization: Bearer ${JWT}" \
  -H "Content-Type: application/json" \
  -d '{"title":"Refactor discussion service","body":"Two code notes below — reply and resolve independently."}')"
[[ "${disc_status}" == "200" || "${disc_status}" == "201" ]] || {
  echo "FAIL: create discussion expected 200/201 got ${disc_status}" >&2
  cat "${WORK_DIR}/disc.json" >&2
  exit 1
}
DISC_NUM="$(json_field "${WORK_DIR}/disc.json" 'd["number"]')"

# Root comment with anchor on service.ts
root_status="$(curl -sS -o "${WORK_DIR}/root.json" -w '%{http_code}' \
  -X POST "${DISC_PATH}/${DISC_NUM}/comments" \
  -H "Authorization: Bearer ${JWT}" \
  -H "Content-Type: application/json" \
  -d "{\"bodyMarkdown\":\"Should \`isResolved\` live here or on the DTO?\",\"anchor\":{\"ref\":\"${default_ref}\",\"commitSha\":\"${commit_sha}\",\"filePath\":\"src/discussions/service.ts\",\"line\":5}}")"
[[ "${root_status}" == "200" ]] || { echo "FAIL: root comment ${root_status}" >&2; exit 1; }
ROOT_ID="$(json_field "${WORK_DIR}/root.json" 'd["id"]["value"] if isinstance(d.get("id"), dict) else d["id"]')"

# Reply on first sub-thread
curl -fsS -o "${WORK_DIR}/reply1.json" \
  -X POST "${DISC_PATH}/${DISC_NUM}/comments" \
  -H "Authorization: Bearer ${JWT}" \
  -H "Content-Type: application/json" \
  -d "{\"bodyMarkdown\":\"Keep it on the root comment row — matches PRD.\",\"parentCommentId\":\"${ROOT_ID}\"}"

# Second root (free-floating)
curl -fsS -o "${WORK_DIR}/root2.json" \
  -X POST "${DISC_PATH}/${DISC_NUM}/comments" \
  -H "Authorization: Bearer ${JWT}" \
  -H "Content-Type: application/json" \
  -d '{"bodyMarkdown":"Docs update: mention sub-thread resolve in README."}'

ROOT2_ID="$(json_field "${WORK_DIR}/root2.json" 'd["id"]["value"] if isinstance(d.get("id"), dict) else d["id"]')"

# Resolve second sub-thread
curl -fsS -o "${WORK_DIR}/resolved.json" \
  -X POST "${DISC_PATH}/comments/${ROOT2_ID}/resolve" \
  -H "Authorization: Bearer ${JWT}" >/dev/null

echo
echo "Discussion sub-thread test repo ready:"
echo "  Owner:       ${TEST_USER}"
echo "  Password:    ${TEST_PASS}"
echo "  Repo:        ${TEST_REPO}"
echo "  Discussion:  #${DISC_NUM}"
echo "  Web:         http://localhost:3000/${TEST_USER}/${TEST_REPO}/discussions/${DISC_NUM}"
echo "  Blob:        http://localhost:3000/${TEST_USER}/${TEST_REPO}/blob/${default_ref}/src/discussions/service.ts"
echo "  List:        http://localhost:3000/${TEST_USER}/${TEST_REPO}/discussions"
