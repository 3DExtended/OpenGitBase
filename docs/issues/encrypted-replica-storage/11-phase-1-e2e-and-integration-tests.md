# Phase 1 E2E and integration tests

## Metadata

- ID: ers-11
- Type: AFK
- Status: ready
- Source: docs/prd/encrypted-replica-storage.md

## Parent

[PRD: Encrypted Replica Storage](../../prd/encrypted-replica-storage.md)

## What to build

Regression protection for Phase 1 four-copy replication at existing HA test seams. Test externally observable behavior, not encryption internals.

**Required scenarios:**

- Push ack gated on encrypted confirmation, not read replica sync
- Read replica serves fetch and web reads; encrypted node rejects git
- Hot promotion of read replica after primary failure
- Cold recovery after primary+read colocated node failure
- Corrupted encrypted artifact rejected during recovery
- Watermark monotonicity and epoch stale rejection unchanged
- RF=3→RF=4 backfill completes without breaking push
- Migration mid-push does not lose data

Extend existing HA chaos and integration test patterns where possible.

## Acceptance criteria

- [ ] Integration tests cover encrypted quorum push happy path and encrypted node down
- [ ] Integration tests cover read routing to read replica with primary fallback
- [ ] Integration tests cover hot promotion with epoch bump
- [ ] Integration tests cover cold recovery producing identical refs
- [ ] Integration tests cover AEAD tamper rejection on recovery
- [ ] E2E or compose chaos scenario covers primary failure with surviving read replica
- [ ] Backfill migration test reaches `Rf4Healthy` on seeded RF=3 repo
- [ ] Tests assert on git outcomes and routing, not cipher text bytes

## Blocked by

- [09-delete-rebalance-and-anti-entropy-extensions.md](./09-delete-rebalance-and-anti-entropy-extensions.md)
- [10-admin-ui-four-copy-replication-status.md](./10-admin-ui-four-copy-replication-status.md)

## User stories covered

- 54 — As a tester, I want integration tests covering push quorum, read routing, hot promotion, cold recovery, and migration backfill, so that behavior is regression-protected.

## Notes

Prior art: HA storage chaos scenarios and quorum integration tests. Disable automatic promotion in test env only where necessary for deterministic setup.
