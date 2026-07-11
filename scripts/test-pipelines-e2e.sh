#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:8089}"
OWNER="${OWNER:-admin}"
REPO_SLUG="${REPO_SLUG:-ci-e2e-$(date +%s)}"
POLL_SECONDS="${POLL_SECONDS:-5}"
POLL_ATTEMPTS="${POLL_ATTEMPTS:-120}"

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

echo "==> Checking API health at ${API_URL}"
curl -fsS "${API_URL}/health" >/dev/null

echo "==> Signing in as ${OWNER}"
LOGIN_RESPONSE="$(curl -fsS -X POST "${API_URL}/api/signin/login" \
  -H "Content-Type: application/json" \
  -d "{\"username\":\"${OWNER}\",\"password\":\"${ADMIN_PASS:-change-me-admin}\"}")"
TOKEN="$(printf '%s' "${LOGIN_RESPONSE}" | tr -d '\n\r"')"
if [ -z "${TOKEN}" ]; then
  echo "Failed to obtain auth token." >&2
  exit 1
fi

echo "==> Creating fixture repository ${OWNER}/${REPO_SLUG}"
CREATE_RESPONSE="$(curl -fsS -X POST "${API_URL}/api/repository/${REPO_SLUG}" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d "{\"repositoryName\":\"${REPO_SLUG}\",\"isPrivate\":false}")"
REPOSITORY_ID="$(printf '%s' "${CREATE_RESPONSE}" | json_get "['value']")"

echo "==> Creating write PAT for git push"
PAT_RESPONSE="$(curl -fsS -X POST "${API_URL}/api/git-access-token" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"name":"ci-e2e","scope":"write","neverExpires":true}')"
PAT_TOKEN="$(printf '%s' "${PAT_RESPONSE}" | json_get "['token']")"

TMP_DIR="$(mktemp -d)"
trap 'rm -rf "${TMP_DIR}"' EXIT

echo "==> Creating local fixture commit"
cat > "${TMP_DIR}/.opengitbase-ci.yml" <<'YAML'
stages:
  - build
  - test

image: alpine:3.20

build:
  stage: build
  runs-on: ogb-hosted
  only:
    - main
  script: |
    echo "build stage"

test:
  stage: test
  runs-on: ogb-hosted
  only:
    - main
  script: |
    echo "test stage"
YAML

git -C "${TMP_DIR}" init -q
git -C "${TMP_DIR}" config user.name "ci-e2e"
git -C "${TMP_DIR}" config user.email "ci-e2e@local.test"
git -C "${TMP_DIR}" add .opengitbase-ci.yml
git -C "${TMP_DIR}" commit -q -m "ci e2e fixture"
AFTER_SHA="$(git -C "${TMP_DIR}" rev-parse HEAD)"

REMOTE_URL="http://${OWNER}:${PAT_TOKEN}@localhost:8089/${OWNER}/${REPO_SLUG}.git"
git -C "${TMP_DIR}" branch -M main
git -C "${TMP_DIR}" remote add origin "${REMOTE_URL}"
echo "==> Pushing fixture commit to ${REMOTE_URL}"
git -C "${TMP_DIR}" push -q -u origin main

echo "==> Waiting for pipeline run ${AFTER_SHA}"
RUN_ID=""
RUN_STATUS=""
for _ in $(seq 1 "${POLL_ATTEMPTS}"); do
  RUNS_JSON="$(curl -fsS "${API_URL}/api/repository/${REPOSITORY_ID}/pipelines" \
    -H "Authorization: Bearer ${TOKEN}")"
  MATCHED="$(printf '%s' "${RUNS_JSON}" | python3 -c "import json,sys; runs=json.load(sys.stdin); sha='${AFTER_SHA}'; m=[r for r in runs if r.get('afterSha')==sha]; print(json.dumps(m[0]) if m else '')")"
  if [ -n "${MATCHED}" ]; then
    RUN_ID="$(printf '%s' "${MATCHED}" | json_get "['id']")"
    RUN_STATUS="$(printf '%s' "${MATCHED}" | json_get "['status']")"
    if [ "${RUN_STATUS}" = "Passed" ] || [ "${RUN_STATUS}" = "Failed" ] || [ "${RUN_STATUS}" = "Cancelled" ]; then
      break
    fi
  fi
  sleep "${POLL_SECONDS}"
done

if [ -z "${RUN_ID}" ]; then
  echo "Pipeline run not found for commit ${AFTER_SHA}." >&2
  exit 1
fi

echo "==> Loading run detail ${RUN_ID}"
RUN_JSON="$(curl -fsS "${API_URL}/api/pipeline/runs/${RUN_ID}" -H "Authorization: Bearer ${TOKEN}")"
RUN_STATUS="$(printf '%s' "${RUN_JSON}" | json_get "['status']")"
JOB_COUNT="$(printf '%s' "${RUN_JSON}" | python3 -c "import json,sys; print(len(json.load(sys.stdin).get('jobs', [])))")"

if [ "${RUN_STATUS}" != "Passed" ]; then
  echo "Pipeline run finished with status ${RUN_STATUS}." >&2
  exit 1
fi

if [ "${JOB_COUNT}" -lt 2 ]; then
  echo "Expected at least 2 jobs, got ${JOB_COUNT}." >&2
  exit 1
fi

echo "==> Verifying logs for each job"
JOB_IDS="$(printf '%s' "${RUN_JSON}" | python3 -c "import json,sys; print('\n'.join(j['id'] for j in json.load(sys.stdin).get('jobs', [])))")"
while IFS= read -r JOB_ID; do
  [ -n "${JOB_ID}" ] || continue
  LOGS_JSON="$(curl -fsS "${API_URL}/api/pipeline/jobs/${JOB_ID}/logs" -H "Authorization: Bearer ${TOKEN}")"
  LOG_COUNT="$(printf '%s' "${LOGS_JSON}" | python3 -c "import json,sys; print(len(json.load(sys.stdin)))")"
  if [ "${LOG_COUNT}" -le 0 ]; then
    echo "Job ${JOB_ID} has no logs." >&2
    exit 1
  fi
done <<< "${JOB_IDS}"

echo "Pipeline E2E passed: run=${RUN_ID} status=${RUN_STATUS} jobs=${JOB_COUNT}"
