#!/usr/bin/env bash
# End-to-end smoke tests for repository web content browsing API.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
API_BASE="${API_BASE:-http://localhost:8089/api}"

echo "==> Repository browse e2e against ${API_BASE}"

fail() {
  echo "FAIL: $*" >&2
  exit 1
}

pass() {
  echo "OK: $*"
}

# Public repo fixture — set via env or skip dependent checks
PUBLIC_OWNER="${PUBLIC_OWNER:-}"
PUBLIC_SLUG="${PUBLIC_SLUG:-}"

if [[ -z "${PUBLIC_OWNER}" || -z "${PUBLIC_SLUG}" ]]; then
  echo "SKIP: set PUBLIC_OWNER and PUBLIC_SLUG to run full public browse checks"
  exit 0
fi

status="$(curl -s -o /tmp/ogb-refs.json -w '%{http_code}' \
  "${API_BASE}/repository/by-slug/${PUBLIC_OWNER}/${PUBLIC_SLUG}/content/refs")"
[[ "${status}" == "200" ]] || fail "public refs expected 200 got ${status}"
pass "public refs 200"

default_ref="$(python3 -c 'import json; print(json.load(open("/tmp/ogb-refs.json")).get("defaultRef") or "")')"
is_empty="$(python3 -c 'import json; print(str(json.load(open("/tmp/ogb-refs.json")).get("isEmpty", False)).lower())')"

if [[ "${is_empty}" == "true" || -z "${default_ref}" ]]; then
  pass "empty repository — skipping tree/readme checks"
  exit 0
fi

pass "defaultRef=${default_ref}"

status="$(curl -s -o /tmp/ogb-tree.json -w '%{http_code}' \
  "${API_BASE}/repository/by-slug/${PUBLIC_OWNER}/${PUBLIC_SLUG}/content/tree?refName=${default_ref}&path=")"
[[ "${status}" == "200" ]] || fail "public tree expected 200 got ${status}"
pass "public tree 200"

cache_header="$(curl -sI "${API_BASE}/repository/by-slug/${PUBLIC_OWNER}/${PUBLIC_SLUG}/content/tree?refName=${default_ref}&path=" | tr -d '\r' | grep -i '^cache-control:' || true)"
echo "${cache_header}" | grep -qi 'public' || fail "expected public Cache-Control"
pass "public Cache-Control"

# Private repo — anonymous should 404
PRIVATE_OWNER="${PRIVATE_OWNER:-}"
PRIVATE_SLUG="${PRIVATE_SLUG:-}"
if [[ -n "${PRIVATE_OWNER}" && -n "${PRIVATE_SLUG}" ]]; then
  status="$(curl -s -o /dev/null -w '%{http_code}' \
    "${API_BASE}/repository/by-slug/${PRIVATE_OWNER}/${PRIVATE_SLUG}/content/refs")"
  [[ "${status}" == "404" ]] || fail "private anonymous refs expected 404 got ${status}"
  pass "private anonymous 404"
fi

echo "All repository browse e2e checks passed."
