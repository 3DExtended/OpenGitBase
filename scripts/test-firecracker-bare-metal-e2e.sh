#!/usr/bin/env bash
set -euo pipefail

# Mandatory KVM bare-metal Firecracker E2E gate for runtime completion sign-off.
if [ ! -e /dev/kvm ]; then
  echo "ERROR: /dev/kvm is required for Firecracker bare-metal E2E." >&2
  exit 1
fi

if ! command -v firecracker >/dev/null 2>&1; then
  echo "ERROR: firecracker binary is required on PATH." >&2
  exit 1
fi

API_URL="${API_URL:-http://localhost:8089}"
OWNER="${OWNER:-admin}"
REPO_SLUG="${REPO_SLUG:-ci-fc-e2e-$(date +%s)}"
POLL_SECONDS="${POLL_SECONDS:-5}"
POLL_ATTEMPTS="${POLL_ATTEMPTS:-120}"
REQUIRE_EXECUTOR="${REQUIRE_EXECUTOR:-FirecrackerMicroVM}"

require_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Missing required command: $1" >&2
    exit 1
  fi
}

json_get() {
  local path="$1"
  python3 -c "import json,sys; print(json.load(sys.stdin)$path)"
}

require_cmd curl
require_cmd git
require_cmd python3

echo "==> KVM and Firecracker prerequisites verified"

LOGIN_RESPONSE="$(curl -fsS -X POST "${API_URL}/api/signin/login" \
  -H "Content-Type: application/json" \
  -d "{\"username\":\"${OWNER}\",\"password\":\"${ADMIN_PASS:-change-me-admin}\"}")"
TOKEN="$(printf '%s' "${LOGIN_RESPONSE}" | tr -d '\n\r"')"

CREATE_RESPONSE="$(curl -fsS -X POST "${API_URL}/api/repository/${REPO_SLUG}" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d "{\"repositoryName\":\"${REPO_SLUG}\",\"isPrivate\":false}")"
REPOSITORY_ID="$(printf '%s' "${CREATE_RESPONSE}" | json_get "['value']")"

PAT_RESPONSE="$(curl -fsS -X POST "${API_URL}/api/git-access-token" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"name":"ci-fc-e2e","scope":"write","neverExpires":true}')"
PAT_TOKEN="$(printf '%s' "${PAT_RESPONSE}" | json_get "['token']")"

TMP_DIR="$(mktemp -d)"
trap 'rm -rf "${TMP_DIR}"' EXIT

cat > "${TMP_DIR}/.opengitbase-ci.yml" <<'YAML'
stages:
  - test

image: alpine:3.20

test:
  stage: test
  runs-on: ogb-hosted
  only:
    - main
  script: |
    echo "firecracker-tracer"
YAML

pushd "${TMP_DIR}" >/dev/null
git init
git config user.email "ci-fc-e2e@example.com"
git config user.name "ci fc e2e"
git add .opengitbase-ci.yml
git commit -m "firecracker tracer"
git branch -M main
git remote add origin "https://${OWNER}:${PAT_TOKEN}@${API_URL#http://}/git/${REPOSITORY_ID}.git"
git push -u origin main
popd >/dev/null

RUN_ID=""
for _ in $(seq 1 "${POLL_ATTEMPTS}"); do
  RUNS="$(curl -fsS "${API_URL}/repository/${REPOSITORY_ID}/pipelines")"
  RUN_ID="$(printf '%s' "${RUNS}" | python3 -c "import json,sys; runs=json.load(sys.stdin); print(runs[0]['id'] if runs else '')")"
  if [ -n "${RUN_ID}" ]; then
    break
  fi
  sleep "${POLL_SECONDS}"
done

if [ -z "${RUN_ID}" ]; then
  echo "ERROR: pipeline run was not created." >&2
  exit 1
fi

for _ in $(seq 1 "${POLL_ATTEMPTS}"); do
  RUN="$(curl -fsS "${API_URL}/pipeline/runs/${RUN_ID}")"
  STATUS="$(printf '%s' "${RUN}" | json_get "['status']")"
  if [ "${STATUS}" = "Passed" ] || [ "${STATUS}" = "Failed" ] || [ "${STATUS}" = "Cancelled" ]; then
    break
  fi
  sleep "${POLL_SECONDS}"
done

RUN="$(curl -fsS "${API_URL}/pipeline/runs/${RUN_ID}")"
STATUS="$(printf '%s' "${RUN}" | json_get "['status']")"
JOB_ID="$(printf '%s' "${RUN}" | python3 -c "import json,sys; run=json.load(sys.stdin); print(run['jobs'][0]['id'])")"
LOGS="$(curl -fsS "${API_URL}/pipeline/jobs/${JOB_ID}/logs")"

if [ "${STATUS}" != "Passed" ]; then
  echo "ERROR: expected Passed run, got ${STATUS}" >&2
  printf '%s\n' "${LOGS}"
  exit 1
fi

if ! printf '%s' "${LOGS}" | grep -q "${REQUIRE_EXECUTOR}"; then
  echo "ERROR: logs do not show MicroVM executor label (${REQUIRE_EXECUTOR})." >&2
  printf '%s\n' "${LOGS}"
  exit 1
fi

echo "Bare-metal Firecracker E2E passed for run ${RUN_ID}."
