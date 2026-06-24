#!/usr/bin/env bash
# End-to-end tests for repository discussions API against local Docker Compose.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
API_BASE="${API_BASE:-http://localhost:8089/api}"
API_URL="${API_URL:-http://localhost:8089}"
SUFFIX="${DISCUSSION_E2E_SUFFIX:-$(date +%s)}"
OWNER_USER="${DISCUSSION_OWNER:-disc-owner-${SUFFIX}}"
READER_USER="${DISCUSSION_READER:-disc-reader-${SUFFIX}}"
OUTSIDER_USER="${DISCUSSION_OUTSIDER:-disc-outsider-${SUFFIX}}"
TEST_PASS="${DISCUSSION_PASS:-Password123!}"
PUBLIC_REPO="${DISCUSSION_PUBLIC_REPO:-disc-public-${SUFFIX}}"
PRIVATE_REPO="${DISCUSSION_PRIVATE_REPO:-disc-private-${SUFFIX}}"

TMP_DIR="$(mktemp -d)"
trap 'rm -rf "${TMP_DIR}"' EXIT

fail() {
  echo "FAIL: $*" >&2
  exit 1
}

pass() {
  echo "OK: $*"
}

json_field() {
  local file="$1"
  local expr="$2"
  python3 -c "import json; d=json.load(open('${file}')); print(${expr})"
}

jwt_user_id() {
  python3 -c '
import base64, json, sys
token = sys.argv[1].strip().strip("\"")
payload = token.split(".")[1]
payload += "=" * (-len(payload) % 4)
claims = json.loads(base64.urlsafe_b64decode(payload))
print(claims["identityproviderid"])
' "$1"
}

register_user() {
  local user="$1"
  curl -sS -o "${TMP_DIR}/register-${user}.json" -w '%{http_code}' \
    -X POST "${API_BASE}/register/register" \
    -H 'Content-Type: application/json' \
    -d "{\"username\":\"${user}\",\"email\":\"${user}@example.com\",\"password\":\"${TEST_PASS}\"}" \
    >/dev/null 2>&1 || true
}

login() {
  local user="$1"
  local status
  status="$(curl -sS -o "${TMP_DIR}/login-${user}.json" -w '%{http_code}' \
    -X POST "${API_BASE}/signin/login" \
    -H 'Content-Type: application/json' \
    -d "{\"username\":\"${user}\",\"password\":\"${TEST_PASS}\"}")"
  [[ "${status}" == "200" ]] || fail "sign-in ${user} expected 200 got ${status}"
  tr -d '\n\r"' < "${TMP_DIR}/login-${user}.json"
}

verify_email() {
  local token="$1"
  curl -sS -o /dev/null \
    -X POST "${API_BASE}/account/debug/verify-email" \
    -H "Authorization: Bearer ${token}" 2>/dev/null || true
}

auth_curl() {
  local token="$1"
  shift
  curl -sS -H "Authorization: Bearer ${token}" "$@"
}

create_repo() {
  local token="$1"
  local slug="$2"
  local name="$3"
  local is_private="$4"
  local attempt status
  for attempt in 1 2 3 4 5 6; do
    status="$(auth_curl "${token}" -o "${TMP_DIR}/repo-${slug}.json" -w '%{http_code}' \
      -X POST "${API_BASE}/repository/${slug}" \
      -H 'Content-Type: application/json' \
      -d "{\"repositoryName\":\"${name}\",\"isPrivate\":${is_private}}")"
    [[ "${status}" == "201" ]] && break
    [[ "${status}" == "503" && "${attempt}" -lt 6 ]] && sleep 5 && continue
    fail "create repo ${slug} expected 201 got ${status}"
  done
  json_field "${TMP_DIR}/repo-${slug}.json" 'd["value"] if isinstance(d, dict) and "value" in d else d'
}

add_member() {
  local token="$1"
  local repo_id="$2"
  local member_id="$3"
  local role="${4:-1}"
  local status
  status="$(auth_curl "${token}" -o /dev/null -w '%{http_code}' \
    -X POST "${API_BASE}/repository-member" \
    -H 'Content-Type: application/json' \
    -d "{\"modelToCreate\":{\"repositoryId\":{\"value\":\"${repo_id}\"},\"userId\":{\"value\":\"${member_id}\"},\"role\":${role}}}")"
  [[ "${status}" == "201" ]] || fail "add member expected 201 got ${status}"
}

echo "==> Repository discussions e2e against ${API_BASE}"

if ! curl -fsS "${API_URL}/health" >/dev/null; then
  fail "API not reachable at ${API_URL}/health — start compose stack first"
fi

register_user "${OWNER_USER}"
register_user "${READER_USER}"
register_user "${OUTSIDER_USER}"

OWNER_TOKEN="$(login "${OWNER_USER}")"
READER_TOKEN="$(login "${READER_USER}")"
OUTSIDER_TOKEN="$(login "${OUTSIDER_USER}")"

verify_email "${OWNER_TOKEN}"
verify_email "${READER_TOKEN}"
verify_email "${OUTSIDER_TOKEN}"

OWNER_ID="$(jwt_user_id "${OWNER_TOKEN}")"
READER_ID="$(jwt_user_id "${READER_TOKEN}")"

PUBLIC_REPO_ID="$(create_repo "${OWNER_TOKEN}" "${PUBLIC_REPO}" "Discussions Public" false)"
PRIVATE_REPO_ID="$(create_repo "${OWNER_TOKEN}" "${PRIVATE_REPO}" "Discussions Private" true)"
add_member "${OWNER_TOKEN}" "${PRIVATE_REPO_ID}" "${READER_ID}" 1

PUBLIC_DISC="${API_BASE}/repository/by-slug/${OWNER_USER}/${PUBLIC_REPO}/discussions"
PRIVATE_DISC="${API_BASE}/repository/by-slug/${OWNER_USER}/${PRIVATE_REPO}/discussions"
PUBLIC_TAGS="${API_BASE}/repository/by-slug/${OWNER_USER}/${PUBLIC_REPO}/tags"
BLOCKED="${API_BASE}/repository/by-slug/${OWNER_USER}/${PUBLIC_REPO}/blocked-users"

# --- public repo: anonymous read ---
status="$(curl -sS -o "${TMP_DIR}/public-list.json" -w '%{http_code}' "${PUBLIC_DISC}")"
[[ "${status}" == "200" ]] || fail "anonymous public list expected 200 got ${status}"
pass "anonymous public list 200"

# --- public repo: anonymous create forbidden ---
status="$(curl -sS -o /dev/null -w '%{http_code}' \
  -X POST "${PUBLIC_DISC}" \
  -H 'Content-Type: application/json' \
  -d '{"title":"anon"}')"
[[ "${status}" == "401" ]] || fail "anonymous create expected 401 got ${status}"
pass "anonymous create 401"

# --- private repo: anonymous 404 ---
status="$(curl -sS -o /dev/null -w '%{http_code}' "${PRIVATE_DISC}")"
[[ "${status}" == "404" ]] || fail "private anonymous list expected 404 got ${status}"
pass "private anonymous 404"

# --- private repo: outsider 403 ---
status="$(auth_curl "${OUTSIDER_TOKEN}" -o /dev/null -w '%{http_code}' "${PRIVATE_DISC}")"
[[ "${status}" == "403" ]] || fail "private outsider list expected 403 got ${status}"
pass "private outsider 403"

# --- private repo: member read ---
status="$(auth_curl "${READER_TOKEN}" -o /dev/null -w '%{http_code}' "${PRIVATE_DISC}")"
[[ "${status}" == "200" ]] || fail "private member list expected 200 got ${status}"
pass "private member list 200"

# --- create discussion + lifecycle ---
status="$(auth_curl "${OWNER_TOKEN}" -o "${TMP_DIR}/disc-create.json" -w '%{http_code}' \
  -X POST "${PUBLIC_DISC}" \
  -H 'Content-Type: application/json' \
  -d '{"title":"E2E discussion","body":"seed"}')"
[[ "${status}" == "201" ]] || fail "create discussion expected 201 got ${status}"
NUMBER="$(json_field "${TMP_DIR}/disc-create.json" 'd["number"]')"
pass "created discussion #${NUMBER}"

status="$(auth_curl "${READER_TOKEN}" -o /dev/null -w '%{http_code}' \
  -X POST "${PUBLIC_DISC}/${NUMBER}/comments" \
  -H 'Content-Type: application/json' \
  -d '{"bodyMarkdown":"reader engages"}')"
[[ "${status}" == "200" ]] || fail "reader comment expected 200 got ${status}"
pass "reader comment engages"

STATUS_VAL="$(auth_curl "${READER_TOKEN}" "${PUBLIC_DISC}/${NUMBER}" | python3 -c 'import json,sys; print(json.load(sys.stdin)["status"])')"
[[ "${STATUS_VAL}" == "1" ]] || fail "expected Engaged status 1 got ${STATUS_VAL}"
pass "status Engaged after non-creator comment"

status="$(auth_curl "${OWNER_TOKEN}" -o /dev/null -w '%{http_code}' \
  -X POST "${PUBLIC_DISC}/${NUMBER}/resolve")"
[[ "${status}" == "200" ]] || fail "resolve expected 200 got ${status}"
STATUS_VAL="$(auth_curl "${OWNER_TOKEN}" "${PUBLIC_DISC}/${NUMBER}" | python3 -c 'import json,sys; print(json.load(sys.stdin)["status"])')"
[[ "${STATUS_VAL}" == "2" ]] || fail "expected Resolved status 2 got ${STATUS_VAL}"
pass "writer resolve from Engaged"

status="$(auth_curl "${READER_TOKEN}" -o /dev/null -w '%{http_code}' \
  -X POST "${PUBLIC_DISC}/${NUMBER}/comments" \
  -H 'Content-Type: application/json' \
  -d '{"bodyMarkdown":"reopen"}')"
[[ "${status}" == "200" ]] || fail "reopen comment expected 200 got ${status}"
STATUS_VAL="$(auth_curl "${READER_TOKEN}" "${PUBLIC_DISC}/${NUMBER}" | python3 -c 'import json,sys; print(json.load(sys.stdin)["status"])')"
[[ "${STATUS_VAL}" == "0" ]] || fail "expected Open after reopen got ${STATUS_VAL}"
pass "reopen to Open without re-Engage"

# --- anchored comment smoke ---
status="$(auth_curl "${OWNER_TOKEN}" -o "${TMP_DIR}/anchor-comment.json" -w '%{http_code}' \
  -X POST "${PUBLIC_DISC}/${NUMBER}/comments" \
  -H 'Content-Type: application/json' \
  -d '{"bodyMarkdown":"anchored note","anchor":{"ref":"main","commitSha":"deadbeef","filePath":"README.md","line":1}}')"
[[ "${status}" == "200" ]] || fail "anchored comment expected 200 got ${status}"
ANCHOR_PATH="$(json_field "${TMP_DIR}/anchor-comment.json" 'd.get("anchor", {}).get("filePath", "")')"
[[ "${ANCHOR_PATH}" == "README.md" ]] || fail "expected anchor filePath README.md got ${ANCHOR_PATH}"
pass "anchored comment stored"

# --- sub-thread: reply, nested list, resolve ---
status="$(auth_curl "${OWNER_TOKEN}" -o "${TMP_DIR}/subthread-disc.json" -w '%{http_code}' \
  -X POST "${PUBLIC_DISC}" \
  -H 'Content-Type: application/json' \
  -d '{"title":"Sub-thread e2e","body":"thread body"}')"
[[ "${status}" == "200" || "${status}" == "201" ]] || fail "sub-thread discussion create expected 200/201 got ${status}"
SUB_NUMBER="$(json_field "${TMP_DIR}/subthread-disc.json" 'd["number"]')"

status="$(auth_curl "${OWNER_TOKEN}" -o "${TMP_DIR}/subthread-root.json" -w '%{http_code}' \
  -X POST "${PUBLIC_DISC}/${SUB_NUMBER}/comments" \
  -H 'Content-Type: application/json' \
  -d '{"bodyMarkdown":"root note"}')"
[[ "${status}" == "200" ]] || fail "sub-thread root expected 200 got ${status}"
ROOT_ID="$(json_field "${TMP_DIR}/subthread-root.json" 'd["id"]["value"] if isinstance(d.get("id"), dict) else d["id"]')"

status="$(auth_curl "${READER_TOKEN}" -o "${TMP_DIR}/subthread-reply.json" -w '%{http_code}' \
  -X POST "${PUBLIC_DISC}/${SUB_NUMBER}/comments" \
  -H 'Content-Type: application/json' \
  -d "{\"bodyMarkdown\":\"reply note\",\"parentCommentId\":\"${ROOT_ID}\"}")"
[[ "${status}" == "200" ]] || fail "sub-thread reply expected 200 got ${status}"

auth_curl "${OWNER_TOKEN}" "${PUBLIC_DISC}/${SUB_NUMBER}/comments" > "${TMP_DIR}/subthread-list.json"
python3 -c "
import json
items = json.load(open('${TMP_DIR}/subthread-list.json'))
root = next(i for i in items if (i.get('id', {}).get('value', i.get('id')) == '${ROOT_ID}' or str(i.get('id')) == '${ROOT_ID}'))
replies = root.get('replies') or []
assert len(replies) == 1, f'expected 1 reply got {len(replies)}'
" || fail "nested replies missing"

status="$(auth_curl "${OWNER_TOKEN}" -o "${TMP_DIR}/subthread-resolved.json" -w '%{http_code}' \
  -X POST "${PUBLIC_DISC}/comments/${ROOT_ID}/resolve")"
[[ "${status}" == "200" ]] || fail "sub-thread resolve expected 200 got ${status}"
RESOLVED="$(json_field "${TMP_DIR}/subthread-resolved.json" 'd.get("isResolved", False)')"
[[ "${RESOLVED}" == "True" ]] || fail "expected isResolved true got ${RESOLVED}"

DISC_STATUS="$(auth_curl "${OWNER_TOKEN}" "${PUBLIC_DISC}/${SUB_NUMBER}" | python3 -c 'import json,sys; print(json.load(sys.stdin)["status"])')"
[[ "${DISC_STATUS}" == "0" ]] || fail "sub-thread resolve must not close discussion, got status ${DISC_STATUS}"
pass "sub-thread reply, nested list, and resolve"

# --- tags: create, assign, filter ---
status="$(auth_curl "${OWNER_TOKEN}" -o "${TMP_DIR}/tag.json" -w '%{http_code}' \
  -X POST "${PUBLIC_TAGS}" \
  -H 'Content-Type: application/json' \
  -d '{"name":"e2e-bug","color":"#ff0000"}')"
[[ "${status}" == "200" ]] || fail "create tag expected 200 got ${status}"
TAG_ID="$(json_field "${TMP_DIR}/tag.json" 'd["id"]["value"] if isinstance(d.get("id"), dict) else d["id"]')"

status="$(auth_curl "${OWNER_TOKEN}" -o /dev/null -w '%{http_code}' \
  -X PATCH "${PUBLIC_DISC}/${NUMBER}" \
  -H 'Content-Type: application/json' \
  -d "{\"tagIds\":[\"${TAG_ID}\"]}")"
[[ "${status}" == "200" ]] || fail "assign tag expected 200 got ${status}"

status="$(auth_curl "${OWNER_TOKEN}" -o "${TMP_DIR}/tag-filter.json" -w '%{http_code}' \
  "${PUBLIC_DISC}?tagId=${TAG_ID}")"
[[ "${status}" == "200" ]] || fail "tag filter expected 200 got ${status}"
FILTER_COUNT="$(python3 -c "import json; print(len(json.load(open('${TMP_DIR}/tag-filter.json'))))")"
[[ "${FILTER_COUNT}" -ge 1 ]] || fail "tag filter returned no discussions"
pass "tag filter smoke"

# --- notifications: creator subscribed, reader comment notifies ---
NOTIFS="$(auth_curl "${OWNER_TOKEN}" "${API_BASE}/notifications?unreadOnly=false")"
echo "${NOTIFS}" | python3 -c "
import json, sys
items = json.load(sys.stdin)
assert any('New comment' in (n.get('message') or '') for n in items), 'missing New comment notification'
print('found notification')
" || fail "creator missing in-app notification"
pass "in-app notification on comment"

# --- blocking: mute participation, preserve read ---
status="$(auth_curl "${OWNER_TOKEN}" -o /dev/null -w '%{http_code}' \
  -X POST "${BLOCKED}" \
  -H 'Content-Type: application/json' \
  -d "{\"userId\":\"${READER_ID}\",\"reason\":\"e2e\"}")"
[[ "${status}" == "200" ]] || fail "block user expected 200 got ${status}"

status="$(auth_curl "${READER_TOKEN}" -o /dev/null -w '%{http_code}' \
  -X POST "${PUBLIC_DISC}/${NUMBER}/comments" \
  -H 'Content-Type: application/json' \
  -d '{"bodyMarkdown":"blocked attempt"}')"
[[ "${status}" == "403" ]] || fail "blocked comment expected 403 got ${status}"

status="$(auth_curl "${READER_TOKEN}" -o /dev/null -w '%{http_code}' "${PUBLIC_DISC}/${NUMBER}")"
[[ "${status}" == "200" ]] || fail "blocked user read expected 200 got ${status}"
pass "blocked user read-only mute"

status="$(auth_curl "${OWNER_TOKEN}" -o /dev/null -w '%{http_code}' \
  -X DELETE "${BLOCKED}/${READER_ID}")"
[[ "${status}" == "204" ]] || fail "unblock expected 204 got ${status}"

status="$(auth_curl "${READER_TOKEN}" -o /dev/null -w '%{http_code}' \
  -X POST "${PUBLIC_DISC}/${NUMBER}/comments" \
  -H 'Content-Type: application/json' \
  -d '{"bodyMarkdown":"restored"}')"
[[ "${status}" == "200" ]] || fail "post-unblock comment expected 200 got ${status}"
pass "unblock restores participation"

echo "All repository discussions e2e checks passed."
