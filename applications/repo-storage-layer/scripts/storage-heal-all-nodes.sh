#!/usr/bin/env bash
# Copyright (c) 2026 OpenGitBase Authors
# SPDX-License-Identifier: LicenseRef-OpenGitBase-1.0
#
# Host-side wrapper to repair every storage node affected by the
# storage-quorum-replicate.sh "Argument list too long" (E2BIG) bug, without
# rebuilding/redeploying the image first.
#
# For each running storage container it:
#   1. copies the FIXED storage-quorum-replicate.sh into the container (so the
#      heal's re-drive works, and ongoing pushes stop failing immediately), and
#   2. copies storage-replication-heal.sh into the container, then
#   3. runs the heal (as the git user) to re-replicate the affected repos.
#
# This is a live hot-patch: a container recreated from the OLD image reverts
# these files, so you MUST still rebuild and redeploy the storage image for a
# permanent fix. This wrapper is only the one-shot repair + interim hotfix.
#
# Run it ON THE UNRAID/DOCKER HOST (needs the docker CLI), from a checkout that
# already contains the fixed scripts (git pull first):
#
#   scripts/storage-heal-all-nodes.sh            # dry run across all storage nodes
#   scripts/storage-heal-all-nodes.sh --apply    # actually heal
#   scripts/storage-heal-all-nodes.sh --apply --repo <repository-id>
#
# Any arguments are forwarded verbatim to storage-replication-heal.sh
# (--apply, --all, --repo <id>). Override which containers are targeted with
# STORAGE_CONTAINER_FILTER (default: names matching "storage").

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPLICATE_SRC="${SCRIPT_DIR}/storage-quorum-replicate.sh"
HEAL_SRC="${SCRIPT_DIR}/storage-replication-heal.sh"
CONTAINER_FILTER="${STORAGE_CONTAINER_FILTER:-storage}"
DEST_DIR="/usr/local/bin"

die() { echo "heal-all: $*" >&2; exit 1; }

command -v docker >/dev/null 2>&1 || die "docker CLI not found; run this on the Docker host."
[ -f "${REPLICATE_SRC}" ] || die "missing ${REPLICATE_SRC} (run 'git pull' in this checkout first)."
[ -f "${HEAL_SRC}" ]      || die "missing ${HEAL_SRC} (run 'git pull' in this checkout first)."

# Guard against copying a stale (pre-fix) replicate script into prod.
if ! grep -q 'ARTIFACT_FILE' "${REPLICATE_SRC}"; then
  die "${REPLICATE_SRC} looks like the OLD (buggy) version — 'git pull' to get the E2BIG fix before running."
fi

# Discover target containers (portable; avoids bash 4+ mapfile).
CONTAINERS=()
while IFS= read -r _name; do
  [ -n "${_name}" ] && CONTAINERS+=("${_name}")
done < <(docker ps --format '{{.Names}}' | grep -Ei "${CONTAINER_FILTER}" | sort)
[ "${#CONTAINERS[@]}" -gt 0 ] || die "no running containers match filter '${CONTAINER_FILTER}'. Set STORAGE_CONTAINER_FILTER."

# Forward remaining args to the in-container heal script, safely quoted.
HEAL_ARGS=""
for a in "$@"; do HEAL_ARGS+=" $(printf '%q' "$a")"; done

APPLYING=0
[[ " $* " == *" --apply "* ]] && APPLYING=1

echo "== heal all storage nodes =="
echo "   containers (${#CONTAINERS[@]}): ${CONTAINERS[*]}"
echo "   mode: $( [ "${APPLYING}" -eq 1 ] && echo 'APPLY' || echo 'DRY-RUN (add --apply to heal)')"
echo

FAILED_NODES=0
for c in "${CONTAINERS[@]}"; do
  echo "---- ${c} ----"

  if ! docker cp "${REPLICATE_SRC}" "${c}:${DEST_DIR}/storage-quorum-replicate.sh"; then
    echo "  ! failed to copy replicate script into ${c}; skipping" >&2
    FAILED_NODES=$((FAILED_NODES + 1)); continue
  fi
  if ! docker cp "${HEAL_SRC}" "${c}:${DEST_DIR}/storage-replication-heal.sh"; then
    echo "  ! failed to copy heal script into ${c}; skipping" >&2
    FAILED_NODES=$((FAILED_NODES + 1)); continue
  fi
  docker exec "${c}" chmod +x \
    "${DEST_DIR}/storage-quorum-replicate.sh" \
    "${DEST_DIR}/storage-replication-heal.sh" 2>/dev/null || true

  # Run the heal as the git user so watermark files keep git:git ownership.
  if docker exec "${c}" su git -s /bin/bash -c \
      "${DEST_DIR}/storage-replication-heal.sh${HEAL_ARGS}"; then
    :
  else
    echo "  ! heal reported failures on ${c}" >&2
    FAILED_NODES=$((FAILED_NODES + 1))
  fi
  echo
done

echo "== all nodes processed; ${FAILED_NODES} node(s) with errors =="
if [ "${APPLYING}" -eq 1 ]; then
  echo "   Reminder: rebuild & redeploy the storage image to make the fix permanent"
  echo "   (this wrapper only hot-patches the currently running containers)."
fi
[ "${FAILED_NODES}" -eq 0 ]
