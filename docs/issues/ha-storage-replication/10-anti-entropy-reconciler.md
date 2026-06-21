# Anti-entropy reconciler

## Metadata

- ID: ha-storage-10
- Type: AFK
- Status: ready
- Source: docs/prd/ha-storage-replication.md

## Parent

[PRD: HA Storage Replication (RF=3)](../../prd/ha-storage-replication.md)

## What to build

Implement a scheduled anti-entropy reconciler (default interval 15–60 minutes, configurable) that scans the fleet for replication drift and repairs it idempotently. Per repository: verify bare repo exists on all expected trio members; compare `AppliedWatermark` vs `PrimaryWatermark` and trigger backfill for lagging replicas; scrub on-disk orphans not present in the DB (e.g. failed async delete scrubs); re-enqueue stalled backfill/rebalance jobs for repos stuck in degraded states.

Read-only with respect to git clients — repairs run in the background.

## Acceptance criteria

- [ ] Reconciler runs on configurable schedule within the API host process or background worker
- [ ] Detects missing bare repo on an expected trio member and enqueues backfill
- [ ] Detects watermark lag beyond threshold and triggers peer sync/backfill
- [ ] Detects on-disk repos without DB records and scrubs orphans safely
- [ ] Re-enqueues stalled jobs for repos in `Degraded` or long-running `RF1Backfilling` state
- [ ] Reconciler actions are idempotent — repeated runs do not corrupt state
- [ ] Unit tests with injected drift scenarios cover missing copy, lagging watermark, and orphan scrub
- [ ] Integration test: simulate failed third-node delete scrub; reconciler removes orphan on next run

## Blocked by

- [07-quorum-delete-and-async-third-scrub.md](./07-quorum-delete-and-async-third-scrub.md)
- [09-automatic-rebalance-and-reattach.md](./09-automatic-rebalance-and-reattach.md)

## User stories covered

- 12 — As an operator, I want a periodic reconciler to detect and repair missing copies, stale watermarks, and orphaned on-disk repos, so that silent replication drift is corrected without waiting for the next push.

## Notes

- v1 uses watermark + existence checks only — not full object-graph hash comparison (per PRD out of scope).
- Metrics/logging for reconciler actions recommended for operability but not blocking.
