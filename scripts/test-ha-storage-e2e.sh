#!/usr/bin/env bash
# End-to-end HA storage integration test (ha-storage-12).
set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
REPO_ROOT=$(cd "${SCRIPT_DIR}/.." && pwd)
# shellcheck source=./docker-env.sh
source "${REPO_ROOT}/scripts/docker-env.sh"

echo "==> HA storage E2E requires a running three-node compose fleet with bootstrap complete."
echo "==> Validating compose services include three storage nodes"
docker compose config --services | rg "^storage-[123]$" | wc -l | rg -q "^3$"

echo "==> Running compose validation script"
"${REPO_ROOT}/scripts/test-ha-storage-compose.sh"

echo "==> Running peer sync integration test"
"${REPO_ROOT}/applications/repo-storage-layer/scripts/peer-sync-integration-test.sh"

echo "==> Running storage quorum replication script checks"
if [[ -x "${REPO_ROOT}/applications/repo-storage-layer/scripts/storage-quorum-replicate.sh" ]]; then
  bash -n "${REPO_ROOT}/applications/repo-storage-layer/scripts/storage-quorum-replicate.sh"
fi

echo "OK: HA storage E2E prerequisites validated"
echo "Manual compose steps:"
echo "  1. Create RF=3 repository and verify bare repos on storage-1/2/3"
echo "  2. Push via dispatcher and verify watermarks on at least two nodes"
echo "  3. Fetch via read routing (primary or in-sync replica)"
echo "  4. Stop primary storage node and verify promotion + resumed push/clone"
echo "  5. Delete repository with one node down and verify quorum delete + async scrub"
