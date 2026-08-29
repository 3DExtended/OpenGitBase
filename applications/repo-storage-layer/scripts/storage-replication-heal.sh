#!/usr/bin/env bash
# Copyright (c) 2026 OpenGitBase Authors
# SPDX-License-Identifier: LicenseRef-OpenGitBase-1.0
#
# Repairs repositories left un-replicated by the storage-quorum-replicate.sh
# "Argument list too long" (E2BIG) bug. That bug aborted the post-receive
# replication hook for any repository whose bundle exceeded ~64 KiB, so the
# push landed on the primary but the encrypted replica never received the
# artifact, quorum was never confirmed, and the local watermark file was left
# advanced past the last confirmed watermark (poisoning the heartbeat).
#
# This script finds repositories where THIS node is primary and the local
# watermark file is ahead of the control plane's confirmed primaryWatermark
# (the bug's signature), rewinds the watermark to the confirmed value, and
# re-drives replication through the (fixed) storage-quorum-replicate.sh.
#
# It is safe to run on every storage node: repositories for which this node is
# not primary are skipped. It is idempotent: a repository that is already
# consistent is left untouched.
#
# Usage:
#   storage-replication-heal.sh            # dry run: report affected repos only
#   storage-replication-heal.sh --apply    # actually heal affected repos
#   storage-replication-heal.sh --apply --repo <repository-id>   # one repo
#   storage-replication-heal.sh --apply --all                    # re-drive every
#                                          # primary repo, not just mismatched ones
#
# Recommended: run as the git user inside the storage container so watermark
# files keep git:git ownership, e.g.
#   su git -s /bin/bash -c /usr/local/bin/storage-replication-heal.sh -- --apply

set -uo pipefail

CONFIG_DIR="${STORAGE_CONFIG_DIR:-/var/lib/opengitbase}"
API_URL="${STORAGE_API_URL:-$(cat "${CONFIG_DIR}/api-url" 2>/dev/null || echo http://api-lb:8080)}"
TOKEN_FILE="${STORAGE_TOKEN_FILE:-${CONFIG_DIR}/api-token}"
NODE_ID="${STORAGE_NODE_ID:-$(cat "${CONFIG_DIR}/node-id" 2>/dev/null || echo "${HOSTNAME:-storage}")}"
NODE_CERT_FILE="${STORAGE_NODE_CERT_FILE:-/etc/opengitbase/node.crt}"
WATERMARK_DIR="${STORAGE_WATERMARK_DIR:-/var/lib/opengitbase/watermarks}"
GIT_ROOT="${STORAGE_GIT_ROOT:-/srv/git}"
REPLICATE_SCRIPT="${STORAGE_REPLICATE_SCRIPT:-/usr/local/bin/storage-quorum-replicate.sh}"

APPLY=0
FORCE_ALL=0
ONLY_REPO=""

while [ $# -gt 0 ]; do
  case "$1" in
    --apply) APPLY=1 ;;
    --all) FORCE_ALL=1 ;;
    --repo) shift; ONLY_REPO="${1:-}" ;;
    --repo=*) ONLY_REPO="${1#--repo=}" ;;
    -h|--help)
      sed -n '2,32p' "$0" | sed 's/^# \{0,1\}//'
      exit 0
      ;;
    *) echo "heal: unknown argument: $1" >&2; exit 2 ;;
  esac
  shift
done

if [ ! -r "${TOKEN_FILE}" ]; then
  echo "heal: API token unavailable at ${TOKEN_FILE}" >&2
  exit 1
fi
TOKEN="$(cat "${TOKEN_FILE}")"

get_certificate_thumbprint() {
  if [ ! -f "${NODE_CERT_FILE}" ]; then
    echo ""
    return
  fi
  openssl x509 -in "${NODE_CERT_FILE}" -noout -fingerprint -sha256 \
    | cut -d= -f2 \
    | tr -d ':' \
    | tr '[:lower:]' '[:upper:]'
}

CERT_THUMBPRINT="$(get_certificate_thumbprint)"
if [ -z "${CERT_THUMBPRINT}" ]; then
  echo "heal: certificate thumbprint unavailable (${NODE_CERT_FILE})" >&2
  exit 1
fi

# Fetch the replication context for a repo. Echoes the raw JSON, or nothing on
# error (caller treats empty as "skip").
fetch_context() {
  local repo_id="$1"
  curl -fsS -m 30 \
    "${API_URL}/api/v1/storage-nodes/repositories/${repo_id}/replication" \
    -H "Authorization: Bearer ${TOKEN}" \
    -H "X-Storage-Node-Id: ${NODE_ID}" \
    -H "X-Storage-Node-Certificate-Thumbprint: ${CERT_THUMBPRINT}" 2>/dev/null
}

json_get() { # <json> <key> <default>
  python3 -c 'import json,sys
try:
    print(json.load(sys.stdin).get(sys.argv[1], sys.argv[2]))
except Exception:
    print(sys.argv[2])' "$2" "$3" <<< "$1"
}

CHECKED=0
AFFECTED=0
HEALED=0
FAILED=0
SKIPPED_NONPRIMARY=0

echo "== storage replication heal =="
echo "   node=${NODE_ID}  api=${API_URL}  git-root=${GIT_ROOT}"
if [ "${APPLY}" -eq 1 ]; then
  echo "   mode=APPLY$( [ "${FORCE_ALL}" -eq 1 ] && echo ' (force re-drive all primaries)')"
else
  echo "   mode=DRY-RUN (re-run with --apply to heal)"
fi
echo

shopt -s nullglob
for repo_path in "${GIT_ROOT}"/*.git; do
  repo_id="$(basename "${repo_path}" .git)"
  [ -n "${ONLY_REPO}" ] && [ "${repo_id}" != "${ONLY_REPO}" ] && continue

  CHECKED=$((CHECKED + 1))
  context="$(fetch_context "${repo_id}")"
  if [ -z "${context}" ]; then
    echo "SKIP  ${repo_id}: no replication context (API error or unknown repo)"
    continue
  fi

  is_primary="$(json_get "${context}" isPrimary False)"
  if [ "${is_primary}" != "True" ]; then
    SKIPPED_NONPRIMARY=$((SKIPPED_NONPRIMARY + 1))
    continue
  fi

  primary_watermark="$(json_get "${context}" primaryWatermark -1)"
  repl_state="$(json_get "${context}" replicationState '')"

  watermark_file="${WATERMARK_DIR}/${repo_id}.txt"
  local_watermark="$(cat "${watermark_file}" 2>/dev/null || echo 0)"
  case "${local_watermark}" in ''|*[!0-9-]*) local_watermark=0 ;; esac
  case "${primary_watermark}" in ''|*[!0-9-]*) primary_watermark=-1 ;; esac

  # Bug signature: local watermark advanced past the confirmed primaryWatermark.
  needs_heal=0
  if [ "${local_watermark}" -gt "${primary_watermark}" ]; then
    needs_heal=1
  fi
  if [ "${FORCE_ALL}" -eq 1 ]; then
    needs_heal=1
  fi

  if [ "${needs_heal}" -eq 0 ]; then
    continue
  fi

  AFFECTED=$((AFFECTED + 1))
  echo "AFFECTED ${repo_id}: local_watermark=${local_watermark} > confirmed=${primary_watermark} state=${repl_state}"

  if [ "${APPLY}" -eq 0 ]; then
    continue
  fi

  # Rewind the local watermark to the last confirmed value so the heartbeat
  # stops over-reporting and the re-drive starts from a correct base. If the
  # re-drive then fails, the repo is left honest (watermark == confirmed).
  if [ "${primary_watermark}" -ge 0 ]; then
    mkdir -p "${WATERMARK_DIR}"
    printf '%s' "${primary_watermark}" > "${watermark_file}"
    chown git:git "${watermark_file}" 2>/dev/null || true
  fi

  echo "  -> re-driving replication via $(basename "${REPLICATE_SCRIPT}") ..."
  if "${REPLICATE_SCRIPT}" "${repo_path}" "" ""; then
    new_local="$(cat "${watermark_file}" 2>/dev/null || echo '?')"
    echo "  -> OK  ${repo_id}: replicated, watermark now ${new_local}"
    HEALED=$((HEALED + 1))
  else
    echo "  -> FAIL ${repo_id}: re-drive failed (watermark left at ${primary_watermark})" >&2
    FAILED=$((FAILED + 1))
  fi
done

echo
echo "== summary =="
echo "   repos checked:        ${CHECKED}"
echo "   non-primary skipped:  ${SKIPPED_NONPRIMARY}"
echo "   affected detected:    ${AFFECTED}"
if [ "${APPLY}" -eq 1 ]; then
  echo "   healed:               ${HEALED}"
  echo "   failed:               ${FAILED}"
  [ "${FAILED}" -eq 0 ]
  exit $?
else
  echo "   (dry run — re-run with --apply to heal the ${AFFECTED} affected repo(s))"
  exit 0
fi
