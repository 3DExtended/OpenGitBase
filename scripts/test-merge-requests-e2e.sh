#!/usr/bin/env bash
# End-to-end checks for merge request workflows against local docker compose.
#
# Prerequisites:
#   docker compose up (API reachable at API_URL, default http://localhost:8089)
#
# Usage:
#   ./scripts/test-merge-requests-e2e.sh
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
API_URL="${API_URL:-http://localhost:8089}"
API_BASE="${API_BASE:-${API_URL}/api}"
SUFFIX="${MERGE_REQUEST_E2E_SUFFIX:-$(date +%s)}"
OWNER_USER="${MERGE_REQUEST_OWNER:-mr-owner-${SUFFIX}}"
WRITER_USER="${MERGE_REQUEST_WRITER:-mr-writer-${SUFFIX}}"
OUTSIDER_USER="${MERGE_REQUEST_OUTSIDER:-mr-outsider-${SUFFIX}}"
TEST_PASS="${MERGE_REQUEST_PASS:-Password123!}"
REPO_SLUG="${MERGE_REQUEST_REPO:-mr-e2e-${SUFFIX}}"

TMP_DIR="$(mktemp -d)"
trap 'rm -rf "${TMP_DIR}"' EXIT

fail() {
  echo "FAIL: $*" >&2
  exit 1
}

step() {
  echo "==> $*"
}

json_field() {
  local file="$1"
  local expr="$2"
  python3 -c "import json; d=json.load(open('${file}')); print(${expr})"
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

register_user() {
  local user="$1"
  curl -sS -o /dev/null -X POST "${API_BASE}/register/register" \
    -H 'Content-Type: application/json' \
    -d "{\"username\":\"${user}\",\"email\":\"${user}@example.com\",\"password\":\"${TEST_PASS}\"}" || true
}

auth_curl() {
  local token="$1"
  shift
  curl -sS -H "Authorization: Bearer ${token}" "$@"
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

create_repo() {
  local token="$1"
  local slug="$2"
  local status
  status="$(auth_curl "${token}" -o "${TMP_DIR}/repo.json" -w '%{http_code}' \
    -X POST "${API_BASE}/repository/${slug}" \
    -H 'Content-Type: application/json' \
    -d "{\"repositoryName\":\"Merge Request E2E\",\"isPrivate\":false}")"
  [[ "${status}" == "201" ]] || fail "create repo expected 201 got ${status}"
  json_field "${TMP_DIR}/repo.json" 'd["value"] if isinstance(d, dict) and "value" in d else d'
}

create_write_pat() {
  local token="$1"
  auth_curl "${token}" -X POST "${API_BASE}/git-access-token" \
    -H 'Content-Type: application/json' \
    -d '{"name":"mr-e2e","scope":"write"}' \
    | python3 -c 'import json,sys; print(json.load(sys.stdin)["token"])'
}

main() {
  if ! curl -fsS "${API_URL}/health" >/dev/null; then
    fail "API not reachable at ${API_URL}/health — start docker compose first"
  fi

  step "Registering users"
  register_user "${OWNER_USER}"
  register_user "${WRITER_USER}"
  register_user "${OUTSIDER_USER}"

  OWNER_TOKEN="$(login "${OWNER_USER}")"
  WRITER_TOKEN="$(login "${WRITER_USER}")"
  OUTSIDER_TOKEN="$(login "${OUTSIDER_USER}")"
  OWNER_ID="$(jwt_user_id "${OWNER_TOKEN}")"
  WRITER_ID="$(jwt_user_id "${WRITER_TOKEN}")"

  step "Verifying emails"
  auth_curl "${OWNER_TOKEN}" -X POST "${API_BASE}/account/debug/verify-email" >/dev/null 2>&1 || true
  auth_curl "${WRITER_TOKEN}" -X POST "${API_BASE}/account/debug/verify-email" >/dev/null 2>&1 || true
  auth_curl "${OUTSIDER_TOKEN}" -X POST "${API_BASE}/account/debug/verify-email" >/dev/null 2>&1 || true

  step "Creating repository and adding writer"
  REPO_ID="$(create_repo "${OWNER_TOKEN}" "${REPO_SLUG}")"
  add_member_status="$(auth_curl "${OWNER_TOKEN}" -o /dev/null -w '%{http_code}' \
    -X POST "${API_BASE}/repository-member" \
    -H 'Content-Type: application/json' \
    -d "{\"modelToCreate\":{\"repositoryId\":{\"value\":\"${REPO_ID}\"},\"userId\":{\"value\":\"${WRITER_ID}\"},\"role\":2}}")"
  [[ "${add_member_status}" == "201" ]] || fail "add writer expected 201 got ${add_member_status}"

  step "Preparing source branch through git push"
  PAT="$(create_write_pat "${OWNER_TOKEN}")"
  REMOTE="http://git:${PAT}@127.0.0.1:8089/${OWNER_USER}/${REPO_SLUG}.git"
  WORK_DIR="${TMP_DIR}/work"
  git -C "${TMP_DIR}" init work -b main >/dev/null
  git -C "${WORK_DIR}" config user.email "${OWNER_USER}@example.com"
  git -C "${WORK_DIR}" config user.name "${OWNER_USER}"
  echo "initial" > "${WORK_DIR}/README.md"
  git -C "${WORK_DIR}" add README.md
  git -C "${WORK_DIR}" commit -m "initial commit" >/dev/null
  git -C "${WORK_DIR}" remote add origin "${REMOTE}"
  git -C "${WORK_DIR}" push -u origin main >/dev/null
  git -C "${WORK_DIR}" checkout -b feature/mr-e2e >/dev/null
  echo "change" >> "${WORK_DIR}/README.md"
  git -C "${WORK_DIR}" add README.md
  git -C "${WORK_DIR}" commit -m "feature commit" >/dev/null
  git -C "${WORK_DIR}" push -u origin feature/mr-e2e >/dev/null

  MR_BASE="${API_BASE}/repository/by-slug/${OWNER_USER}/${REPO_SLUG}/merge-requests"

  step "Checking branch-ahead-summary for feature branch"
  ahead_status="$(auth_curl "${OWNER_TOKEN}" -o "${TMP_DIR}/ahead.json" -w '%{http_code}' \
    "${MR_BASE}/branch-ahead-summary?ref=feature/mr-e2e")"
  [[ "${ahead_status}" == "200" ]] || fail "branch-ahead-summary expected 200 got ${ahead_status}"
  ahead_count="$(json_field "${TMP_DIR}/ahead.json" 'd.get("aheadCount", 0)')"
  [[ "${ahead_count}" -ge 1 ]] || fail "branch-ahead-summary expected aheadCount >= 1 got ${ahead_count}"

  step "Creating merge request and posting overview comment"
  create_mr_status="$(auth_curl "${OWNER_TOKEN}" -o "${TMP_DIR}/mr.json" -w '%{http_code}' \
    -X POST "${MR_BASE}" \
    -H 'Content-Type: application/json' \
    -d '{"title":"E2E merge request","body":"Related work for branch protection","sourceRef":"feature/mr-e2e","targetRef":"main","isDraft":false}')"
  [[ "${create_mr_status}" == "200" || "${create_mr_status}" == "201" ]] || fail "create merge request expected 200/201 got ${create_mr_status}"
  MR_NUMBER="$(json_field "${TMP_DIR}/mr.json" 'd["number"]')"

  comment_status="$(auth_curl "${WRITER_TOKEN}" -o /dev/null -w '%{http_code}' \
    -X POST "${MR_BASE}/${MR_NUMBER}/comments" \
    -H 'Content-Type: application/json' \
    -d '{"bodyMarkdown":"overview comment from writer"}')"
  [[ "${comment_status}" == "200" || "${comment_status}" == "201" ]] || fail "overview comment expected 200/201 got ${comment_status}"

  step "Verifying auth matrix (public read, unauthenticated create denied, outsider write denied)"
  public_read_status="$(curl -sS -o /dev/null -w '%{http_code}' "${MR_BASE}/${MR_NUMBER}")"
  [[ "${public_read_status}" == "200" ]] || fail "anonymous read expected 200 got ${public_read_status}"

  anon_create_status="$(curl -sS -o /dev/null -w '%{http_code}' \
    -X POST "${MR_BASE}" \
    -H 'Content-Type: application/json' \
    -d '{"title":"anon","sourceRef":"feature/mr-e2e","targetRef":"main"}')"
  [[ "${anon_create_status}" == "401" ]] || fail "anonymous create expected 401 got ${anon_create_status}"

  outsider_write_status="$(auth_curl "${OUTSIDER_TOKEN}" -o /dev/null -w '%{http_code}' \
    -X POST "${MR_BASE}/${MR_NUMBER}/comments" \
    -H 'Content-Type: application/json' \
    -d '{"bodyMarkdown":"outsider comment"}')"
  [[ "${outsider_write_status}" == "403" || "${outsider_write_status}" == "404" ]] || fail "outsider write expected 403/404 got ${outsider_write_status}"

  step "Checking changes, commits, and linked discussions endpoints"
  changes_status="$(auth_curl "${OWNER_TOKEN}" -o /dev/null -w '%{http_code}' "${MR_BASE}/${MR_NUMBER}/changes")"
  [[ "${changes_status}" == "200" ]] || fail "changes endpoint expected 200 got ${changes_status}"

  commits_status="$(auth_curl "${OWNER_TOKEN}" -o /dev/null -w '%{http_code}' "${MR_BASE}/${MR_NUMBER}/commits")"
  [[ "${commits_status}" == "200" ]] || fail "commits endpoint expected 200 got ${commits_status}"

  links_status="$(auth_curl "${OWNER_TOKEN}" -o /dev/null -w '%{http_code}' "${MR_BASE}/${MR_NUMBER}/discussion-links")"
  [[ "${links_status}" == "200" ]] || fail "discussion-links endpoint expected 200 got ${links_status}"

  step "Protecting main and verifying protected branch rules API"
  protect_status="$(auth_curl "${OWNER_TOKEN}" -o /dev/null -w '%{http_code}' \
    -X POST "${API_BASE}/repository/${REPO_ID}/protected-branch-rules" \
    -H 'Content-Type: application/json' \
    -d '{"pattern":"main","blockDirectPush":true,"allowedPushRoles":2,"requiredApprovalCount":0,"mergeRoleThreshold":2,"forcePushPolicy":0,"pushRules":[{"ruleType":3,"configJson":"{\"required\":true}"}]}')"
  [[ "${protect_status}" == "201" ]] || fail "create protected branch rule expected 201 got ${protect_status}"

  step "Writer direct push to protected main should be rejected"
  git -C "${WORK_DIR}" checkout main >/dev/null
  echo "blocked-direct" >> "${WORK_DIR}/README.md"
  git -C "${WORK_DIR}" add README.md
  git -C "${WORK_DIR}" commit -m "Signed-off-by: ${WRITER_USER} <${WRITER_USER}@example.com>" >/dev/null || true
  writer_pat="$(create_write_pat "${WRITER_TOKEN}")"
  writer_remote="http://git:${writer_pat}@127.0.0.1:8089/${OWNER_USER}/${REPO_SLUG}.git"
  if git -C "${WORK_DIR}" push "${writer_remote}" main >/dev/null 2>"${TMP_DIR}/push-main.err"; then
    fail "writer direct push to protected main should have been rejected"
  fi
  grep -qi "denied\|rejected\|forbidden\|protected\|dco\|push rule" "${TMP_DIR}/push-main.err" \
    || fail "expected push rejection message substring in: $(cat "${TMP_DIR}/push-main.err")"

  step "Merge request e2e checks passed"
}

main "$@"
