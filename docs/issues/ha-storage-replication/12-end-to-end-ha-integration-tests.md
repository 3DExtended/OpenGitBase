# End-to-end HA integration tests

## Metadata

- ID: ha-storage-12
- Type: AFK
- Status: ready
- Source: docs/prd/ha-storage-replication.md

## Parent

[PRD: HA Storage Replication (RF=3)](../../prd/ha-storage-replication.md)

## What to build

Add compose-based end-to-end integration tests that exercise the full HA storage path across all prior slices. Cover: RF=3 repository create (bare repos on three nodes); quorum push with watermark increment on two nodes; fetch/clone via in-sync replica; simulated primary node failure with automatic promotion and resumed push; automatic rebalance when a node becomes unhealthy; quorum delete with one node down; reconciler repair of injected drift (stretch if harness supports scheduling).

Tests should follow prior art from git storage proxy e2e and repo-storage-layer integration tests.

## Acceptance criteria

- [ ] E2E test creates repository and verifies bare repos exist on three storage nodes
- [ ] E2E test pushes commits and verifies watermarks on at least two nodes
- [ ] E2E test clones/fetches via read path hitting a non-primary in-sync replica (or verifies read routing metadata)
- [ ] E2E test stops primary storage node and verifies push/clone resume after promotion
- [ ] E2E test stops one non-primary node and verifies push still succeeds (quorum 2/3)
- [ ] E2E test deletes repository with one node down and verifies DB and disk cleanup
- [ ] E2E test documents required compose profile (three storage nodes, bootstrap complete)
- [ ] Tests run in CI or are documented as manual compose tests if CI lacks three-node resources

## Blocked by

- [10-anti-entropy-reconciler.md](./10-anti-entropy-reconciler.md)
- [11-admin-ui-replication-status.md](./11-admin-ui-replication-status.md)

## User stories covered

- 34 — As a tester, I want integration tests that verify push quorum, read routing to in-sync replicas, primary promotion, and automatic rebalance, so that HA behavior is regression-protected.

## Notes

- This slice is intentionally last — it validates the full stack, not individual modules.
- Reconciler e2e may use a test hook to trigger reconciliation immediately rather than waiting for schedule interval.
- Prior art: `docs/issues/git-storage-proxy/05-network-isolation-and-e2e-integration-tests.md`, `repo-storage-layer/scripts/integration-test.sh`.
