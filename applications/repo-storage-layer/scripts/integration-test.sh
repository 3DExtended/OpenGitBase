#!/usr/bin/env bash
#
# Integration test for repo-storage-layer: build image, start container,
# push a git repo over SSH, verify via volume inspection and git clone.
#
# Usage:
#   applications/repo-storage-layer/scripts/integration-test.sh
#   # or from applications/repo-storage-layer:
#   ./scripts/integration-test.sh
#
# Requires: docker, git, ssh-keygen, mktemp, nc

set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
APP_DIR=$(cd "${SCRIPT_DIR}/.." && pwd)
REPO_ROOT=$(cd "${APP_DIR}/../.." && pwd)
# shellcheck source=../../../scripts/docker-env.sh
source "${REPO_ROOT}/scripts/docker-env.sh"

# Test constants — fixed values make assertions deterministic across runs.
IMAGE_TAG="opengitbase/repo-storage-layer:test"
REPO_NAME="integration-test.git"
COMMIT_SUBJECT="integration test commit"
README_CONTENT="OpenGitBase repo-storage-layer integration test"

# Set during the run; used by the EXIT trap for cleanup.
CONTAINER_NAME=""
TMP=""

require_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "error: required command not found: $1" >&2
    exit 1
  fi
}

# Always tear down the container and temp dir, even when a step fails.
cleanup() {
  if [ -n "${CONTAINER_NAME}" ]; then
    docker rm -f "${CONTAINER_NAME}" >/dev/null 2>&1 || true
  fi
  if [ -n "${TMP}" ] && [ -d "${TMP}" ]; then
    rm -rf "${TMP}"
  fi
}

# Wait until sshd accepts TCP connections. We probe with nc rather than
# `ssh … true` because the git user's shell is git-shell, which only accepts
# git commands (upload-pack/receive-pack) and rejects arbitrary commands.
wait_for_ssh_port() {
  local port=$1 attempt
  for attempt in $(seq 1 30); do
    if nc -z 127.0.0.1 "${port}" 2>/dev/null; then
      return 0
    fi
    sleep 1
  done
  echo "error: SSH port ${port} did not become reachable" >&2
  return 1
}

require_cmd docker
require_cmd git
require_cmd ssh-keygen
require_cmd mktemp
require_cmd nc

TMP=$(mktemp -d)
trap cleanup EXIT

# Temp layout mirrors the container's volume mounts:
#   ssh/   → /home/git/.ssh   (authorized_keys for pubkey auth)
#   repos/ → /srv/git          (bare repository root)
#   keys/  → ephemeral client keypair (never committed)
#   work/  → local repo we push from the host
#   clone/ → created later by git clone verification
mkdir -p "${TMP}/ssh" "${TMP}/repos" "${TMP}/keys" "${TMP}/work"

# Generate client keys and publish the public key before `docker run` so the
# entrypoint can chown/chmod authorized_keys on first start — no restart needed.
KEY="${TMP}/keys/id_ed25519"
ssh-keygen -t ed25519 -N "" -f "${KEY}" -q
cat "${KEY}.pub" > "${TMP}/ssh/authorized_keys"
chmod 600 "${TMP}/ssh/authorized_keys"

echo "==> Building Docker image ${IMAGE_TAG}"
docker build -t "${IMAGE_TAG}" "${APP_DIR}"

CONTAINER_NAME="repo-storage-it-${RANDOM}"
echo "==> Starting container ${CONTAINER_NAME}"

# -p 0:22 picks a free host port to avoid collisions with other SSH services.
docker run -d --name "${CONTAINER_NAME}" \
  -p 0:22 \
  -v "${TMP}/ssh:/home/git/.ssh" \
  -v "${TMP}/repos:/srv/git" \
  "${IMAGE_TAG}" >/dev/null

# docker port may return multiple lines (IPv4 + IPv6); take the first host port.
PORT=$(docker port "${CONTAINER_NAME}" 22/tcp | sed -E 's/.*:([0-9]+)$/\1/' | head -1)
if [ -z "${PORT}" ]; then
  echo "error: could not discover mapped SSH port" >&2
  exit 1
fi
echo "==> SSH available on 127.0.0.1:${PORT}"

echo "==> Waiting for SSH port"
wait_for_ssh_port "${PORT}"

# git-shell only accepts pushes to existing bare repos — create one server-side.
# Run as the git user so ownership inside /srv/git stays correct.
echo "==> Pre-creating bare repository"
docker exec -u git "${CONTAINER_NAME}" git init --bare -b main "/srv/git/${REPO_NAME}" >/dev/null

# GIT_SSH_COMMAND is used by git for all SSH transport (push and clone).
# StrictHostKeyChecking=no is acceptable here: ephemeral container, local test only.
GIT_SSH_COMMAND="ssh -i ${KEY} -p ${PORT} -o BatchMode=yes -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null"
REMOTE_URL="ssh://git@127.0.0.1:${PORT}/srv/git/${REPO_NAME}"

echo "==> Creating local repository and pushing"
git -C "${TMP}/work" init -b main >/dev/null
printf '%s\n' "${README_CONTENT}" > "${TMP}/work/README"
git -C "${TMP}/work" add README

# Local git identity only for this temp repo; not written to global config.
git -C "${TMP}/work" -c user.email="test@opengitbase.local" -c user.name="Integration Test" \
  commit -m "${COMMIT_SUBJECT}" >/dev/null
GIT_SSH_COMMAND="${GIT_SSH_COMMAND}" git -C "${TMP}/work" push "${REMOTE_URL}" main

# Verification 1: inspect the bind-mounted bare repo directly on the host.
# This confirms receive-pack wrote refs/objects without another SSH round-trip.
echo "==> Verifying bare repository on volume"
BARE_REPO="${TMP}/repos/${REPO_NAME}"
if [ ! -f "${BARE_REPO}/refs/heads/main" ]; then
  echo "error: refs/heads/main not found in bare repository" >&2
  exit 1
fi

# Target branch explicitly — bare HEAD may not match the pushed branch name.
VOLUME_SUBJECT=$(git --git-dir="${BARE_REPO}" log -1 main --format=%s)
if [ "${VOLUME_SUBJECT}" != "${COMMIT_SUBJECT}" ]; then
  echo "error: volume commit subject mismatch (got: ${VOLUME_SUBJECT})" >&2
  exit 1
fi

# Verification 2: clone back over SSH and compare file content end-to-end.
echo "==> Verifying clone over SSH"
GIT_SSH_COMMAND="${GIT_SSH_COMMAND}" git clone "${REMOTE_URL}" "${TMP}/clone" >/dev/null
if ! diff -q "${TMP}/work/README" "${TMP}/clone/README" >/dev/null; then
  echo "error: cloned README does not match pushed content" >&2
  exit 1
fi

echo "integration test passed"
