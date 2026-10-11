#!/usr/bin/env bash
# Copyright (c) 2026 OpenGitBase Authors
# SPDX-License-Identifier: LicenseRef-OpenGitBase-1.0
#
# Replicates a write on a repository's primary, then hands the updated refs to pipelines.
#
# Usage:
#   storage-quorum-replicate.sh <physical-path> --post-receive   # the post-receive hook: reads
#                                                                # "<old> <new> <ref>" lines on stdin
#   storage-quorum-replicate.sh <physical-path> [<ref> <sha>]    # one ref (hooks written before
#                                                                # #413), or replication only
#
# Replication runs once per push. The API builds the encrypted artifact on this primary and
# uploads it to the encrypted replicas itself (quorum-replicate with createArtifact). This script
# sends no key, bundle or artifact: those used to travel hex-encoded through the API's request
# body, which failed with HTTP 413 once a bundle passed ~15 MB (#413), and the key used to sit on
# a command line.
#
# Watermark file - a deliberate decision: before replicating, the file is raised to the watermark
# this write will commit (the API's PrimaryWatermark + 1), and this script never lowers it. When
# replication fails, the heartbeat reports that watermark, the API sees the primary ahead of its
# quorum, and the anti-entropy reconciler replicates the write once the push's grace period is
# over. This script used to roll the file back on failure, which hid the failure from everything.
# When the API cannot be asked at all, the file is raised by one; a run of such failures raises it
# further, and the reconciler then commits one step per pass until it has caught up (each step
# replicates the whole repository, so nothing is lost meanwhile).
#
# Pipelines: every updated ref goes to git-push-ingest once replication has been tried, whether it
# succeeded or not - the push is on the primary either way. Exception: storage_merge.py sets
# STORAGE_HOOK_ROLLBACK_ON_FAILURE=1 and undoes the ref update when this script fails, so nothing
# is ingested after a failure then.
#
# Exit status: 0 when the write is replicated or this node is not the primary, 1 otherwise. git
# ignores a post-receive hook's status; storage_merge.py does not. Every failure prints one line
# to stderr, which git shows the pusher as "remote: ...". Runs on bash 3.2.

set -uo pipefail

PROG="storage-quorum-replicate"
PHYSICAL_PATH="${1:-}"
MODE="single"
PUSH_REF=""
AFTER_SHA=""
if [ "${2:-}" = "--post-receive" ]; then
  MODE="post-receive"
else
  PUSH_REF="${2:-}"
  AFTER_SHA="${3:-}"
fi

CONFIG_DIR="${STORAGE_CONFIG_DIR:-/var/lib/opengitbase}"
API_URL="${STORAGE_API_URL:-$(cat "${CONFIG_DIR}/api-url" 2>/dev/null || echo http://api-lb:8080)}"
TOKEN_FILE="${STORAGE_TOKEN_FILE:-${CONFIG_DIR}/api-token}"
NODE_ID="${STORAGE_NODE_ID:-$(cat "${CONFIG_DIR}/node-id" 2>/dev/null || echo "${HOSTNAME:-storage}")}"
NODE_CERT_FILE="${STORAGE_NODE_CERT_FILE:-/etc/opengitbase/node.crt}"
WATERMARK_DIR="${STORAGE_WATERMARK_DIR:-/var/lib/opengitbase/watermarks}"
# The API builds and uploads the whole encrypted repository inside this one call.
QUORUM_TIMEOUT_SECONDS="${STORAGE_QUORUM_TIMEOUT_SECONDS:-900}"
# Retries for the idempotent calls (context, ingest), e.g. across an API instance restarting.
API_RETRIES="${STORAGE_API_RETRIES:-2}"
ROLLBACK_ON_FAILURE="${STORAGE_HOOK_ROLLBACK_ON_FAILURE:-0}"

say() {
  printf '%s: %s\n' "${PROG}" "$*" >&2
}

if [ -z "${PHYSICAL_PATH}" ]; then
  say "physical path is required"
  exit 1
fi

REPO_ID="$(basename "${PHYSICAL_PATH}" .git)"
WATERMARK_FILE="${WATERMARK_DIR}/${REPO_ID}.txt"

# The updated refs, read before anything can fail so a failed replication still ingests them.
REF_NAMES=()
REF_SHAS=()
if [ "${MODE}" = "post-receive" ]; then
  while read -r _old_sha new_sha ref_name; do
    [ -n "${ref_name:-}" ] || continue
    REF_NAMES[${#REF_NAMES[@]}]="${ref_name}"
    REF_SHAS[${#REF_SHAS[@]}]="${new_sha}"
  done
elif [ -n "${PUSH_REF}" ] && [ -n "${AFTER_SHA}" ]; then
  REF_NAMES[0]="${PUSH_REF}"
  REF_SHAS[0]="${AFTER_SHA}"
fi

WORK_DIR="$(mktemp -d "${TMPDIR:-/tmp}/storage-quorum-replicate.XXXXXX" 2>/dev/null)"
if [ -z "${WORK_DIR}" ] || [ ! -d "${WORK_DIR}" ]; then
  say "${REPO_ID} stored on the primary only; no temporary directory for the replication call - the reconciler will retry"
  exit 1
fi
trap 'rm -rf "${WORK_DIR}"' EXIT
HEADER_FILE="${WORK_DIR}/headers"
CURL_ERROR_FILE="${WORK_DIR}/curl-error"
: > "${HEADER_FILE}"

# Prints one top-level field of a JSON file, or <default> when the file is not JSON or lacks it.
# Booleans print as true/false, whitespace collapses to single spaces; never a traceback.
json_field() {
  python3 - "$1" "$2" "$3" 2>/dev/null <<'PY' || printf '%s\n' "$3"
import json
import sys

path, key, default = sys.argv[1:4]
try:
    with open(path, encoding="utf-8") as handle:
        value = json.load(handle).get(key)
except Exception:
    value = None
if value is None:
    value = default
elif isinstance(value, bool):
    value = "true" if value else "false"
print(" ".join(str(value).split()))
PY
}

# Calls the API: <method> <path> <body-file or ""> <timeout-seconds> <response-file> <retries>.
# Prints the HTTP status, 000 when no answer came. Credentials go through a header file, so the
# token is not on curl's command line.
api_call() {
  local method="$1" path="$2" body="$3" timeout="$4" out="$5" retries="$6" code
  local args
  args=(-sS -m "${timeout}" -X "${method}" -o "${out}" -w '%{http_code}' -H @"${HEADER_FILE}")
  if [ "${retries}" -gt 0 ]; then
    args=("${args[@]}" --retry "${retries}" --retry-delay 2 --retry-connrefused)
  fi
  if [ -n "${body}" ]; then
    args=("${args[@]}" -H 'Content-Type: application/json' --data-binary @"${body}")
  fi
  : > "${out}"
  code="$(curl "${args[@]}" "${API_URL}${path}" 2>"${CURL_ERROR_FILE}")"
  case "${code}" in
    [0-9][0-9][0-9]) ;;
    *) code="000" ;;
  esac
  printf '%s' "${code}"
}

# One-line description of a failed call: "HTTP 413", "HTTP 409: <error>", "API unreachable: ...".
describe_failure() {
  local code="$1" out="$2" detail
  if [ "${code}" = "000" ]; then
    detail="$(tail -n 1 "${CURL_ERROR_FILE}" 2>/dev/null | sed 's/^curl: ([0-9]*) //')"
    printf 'API unreachable%s' "${detail:+: ${detail}}"
    return
  fi
  detail="$(json_field "${out}" error '')"
  [ -n "${detail}" ] || detail="$(json_field "${out}" title '')"
  printf 'HTTP %s%s' "${code}" "${detail:+: ${detail}}"
}

read_watermark() {
  local value
  value="$(cat "${WATERMARK_FILE}" 2>/dev/null || true)"
  case "${value}" in
    '' | *[!0-9]*) value=0 ;;
  esac
  printf '%s' "${value}"
}

# Raises the watermark file to <value>; never lowers it.
raise_watermark() {
  local target="$1"
  if [ "${target}" -le "$(read_watermark)" ]; then
    return 0
  fi
  mkdir -p "${WATERMARK_DIR}" 2>/dev/null
  if ! { printf '%s' "${target}" > "${WATERMARK_FILE}"; } 2>/dev/null; then
    say "${REPO_ID}: cannot write the watermark file ${WATERMARK_FILE}; the reconciler will not see this write"
  fi
}

certificate_thumbprint() {
  [ -f "${NODE_CERT_FILE}" ] || return 0
  openssl x509 -in "${NODE_CERT_FILE}" -noout -fingerprint -sha256 2>/dev/null \
    | cut -d= -f2 \
    | tr -d ':' \
    | tr '[:lower:]' '[:upper:]'
}

NOT_PRIMARY=0
STATE=""
FAILURE=""

# Replicates the write; on failure sets FAILURE to the reason and returns 1.
replicate() {
  if [ ! -r "${TOKEN_FILE}" ]; then
    raise_watermark $(($(read_watermark) + 1))
    FAILURE="API token unavailable (${TOKEN_FILE})"
    return 1
  fi
  local token thumbprint
  token="$(cat "${TOKEN_FILE}")"
  thumbprint="$(certificate_thumbprint)"
  if [ -z "${thumbprint}" ]; then
    raise_watermark $(($(read_watermark) + 1))
    FAILURE="node certificate thumbprint unavailable (${NODE_CERT_FILE})"
    return 1
  fi
  (
    umask 077
    printf 'Authorization: Bearer %s\nX-Storage-Node-Id: %s\nX-Storage-Node-Certificate-Thumbprint: %s\n' \
      "${token}" "${NODE_ID}" "${thumbprint}" > "${HEADER_FILE}"
  )

  local context="${WORK_DIR}/context.json" response="${WORK_DIR}/response.json"
  local request="${WORK_DIR}/request.json" attempt=1 code primary_watermark target committed
  while :; do
    code="$(api_call GET "/api/v1/storage-nodes/repositories/${REPO_ID}/replication" "" 30 "${context}" "${API_RETRIES}")"
    primary_watermark="$(json_field "${context}" primaryWatermark '')"
    case "${primary_watermark}" in
      '' | *[!0-9]*) primary_watermark="" ;;
    esac
    if [ "${code}" != "200" ] || [ -z "${primary_watermark}" ]; then
      raise_watermark $(($(read_watermark) + 1))
      FAILURE="replication context unavailable ($(describe_failure "${code}" "${context}"))"
      return 1
    fi

    if [ "$(json_field "${context}" isPrimary false)" != "true" ]; then
      NOT_PRIMARY=1
      return 0
    fi

    STATE="$(json_field "${context}" replicationState '')"
    target=$((primary_watermark + 1))
    raise_watermark "${target}"

    if [ "${STATE}" = "Rf4Healthy" ] && [ "$(json_field "${context}" apiCreatesArtifacts false)" != "true" ]; then
      # An API from before #413 would ignore createArtifact and refuse the write for lack of
      # confirmed encrypted replicas. The API is rolled out before the storage nodes.
      FAILURE="the API cannot build encrypted artifacts yet; update the API before the storage nodes"
      return 1
    fi

    printf '{"appliedWatermark": %s, "createArtifact": true}' "${target}" > "${request}"
    code="$(api_call POST "/api/v1/storage-nodes/repositories/${REPO_ID}/quorum-replicate" \
      "${request}" "${QUORUM_TIMEOUT_SECONDS}" "${response}" 0)"
    if [ "${code}" = "200" ] && [ "$(json_field "${response}" success false)" = "true" ]; then
      committed="$(json_field "${response}" primaryWatermark "${target}")"
      case "${committed}" in
        '' | *[!0-9]*) committed="${target}" ;;
      esac
      raise_watermark "${committed}"
      return 0
    fi

    # Stale: the watermark was committed meanwhile (another push, or the reconciler), maybe
    # without this push's commits. Ask again with the next one, once.
    if [ "${code}" = "409" ] && [ "${attempt}" -eq 1 ] && [ "$(json_field "${response}" stale false)" = "true" ]; then
      attempt=2
      continue
    fi

    if [ "${STATE}" = "Rf4Healthy" ]; then
      FAILURE="encrypted replication failed ($(describe_failure "${code}" "${response}"))"
    else
      FAILURE="replication failed ($(describe_failure "${code}" "${response}"))"
    fi
    return 1
  done
}

# Hands every updated ref to pipelines; one line per ref that did not get there.
ingest_refs() {
  local index=0 payload="${WORK_DIR}/ingest.json" out="${WORK_DIR}/ingest-response" code
  local retry_args=()
  if [ "${API_RETRIES}" -gt 0 ]; then
    retry_args=(--retry "${API_RETRIES}" --retry-delay 1 --retry-connrefused)
  fi
  while [ "${index}" -lt "${#REF_NAMES[@]}" ]; do
    if ! python3 -c 'import json, sys; print(json.dumps({"repositoryId": sys.argv[1], "ref": sys.argv[2], "afterSha": sys.argv[3]}))' \
      "${REPO_ID}" "${REF_NAMES[${index}]}" "${REF_SHAS[${index}]}" > "${payload}" 2>/dev/null; then
      say "${REPO_ID} ${REF_NAMES[${index}]} not handed to pipelines (python3 unavailable)"
      index=$((index + 1))
      continue
    fi
    : > "${out}"
    code="$(curl -sS -m 10 -X POST -o "${out}" -w '%{http_code}' ${retry_args[@]+"${retry_args[@]}"} \
      -H 'Content-Type: application/json' -H "X-Storage-Node-Id: ${NODE_ID}" --data-binary @"${payload}" \
      "${API_URL}/api/v1/internal/pipelines/git-push-ingest" 2>"${CURL_ERROR_FILE}")"
    case "${code}" in
      2[0-9][0-9]) ;;
      [0-9][0-9][0-9]) say "${REPO_ID} ${REF_NAMES[${index}]} not handed to pipelines ($(describe_failure "${code}" "${out}"))" ;;
      *) say "${REPO_ID} ${REF_NAMES[${index}]} not handed to pipelines ($(describe_failure 000 "${out}"))" ;;
    esac
    index=$((index + 1))
  done
}

STATUS=0
if ! replicate; then
  STATUS=1
  say "${REPO_ID} stored on the primary only; ${FAILURE} - the reconciler will retry"
fi

if [ "${NOT_PRIMARY}" -eq 1 ]; then
  exit 0
fi

if [ "${STATUS}" -eq 0 ] || [ "${ROLLBACK_ON_FAILURE}" != "1" ]; then
  ingest_refs
fi

exit "${STATUS}"
