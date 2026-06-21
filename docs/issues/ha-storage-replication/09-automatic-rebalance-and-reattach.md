# Automatic rebalance and reattach

## Metadata

- ID: ha-storage-09
- Type: AFK
- Status: ready
- Source: docs/prd/ha-storage-replication.md

## Parent

[PRD: HA Storage Replication (RF=3)](../../prd/ha-storage-replication.md)

## What to build

When a storage node becomes unhealthy beyond the heartbeat threshold or is explicitly deregistered, automatically assign a replacement node to affected replica sets and backfill missing copies from the current primary. Never treat a repository as healthy with fewer than two durable copies.

**Reattach behavior (confirmed):** if the original node recovers before the replacement reaches in-sync status, cancel the in-flight replacement and restore the original trio membership. If the replacement already reached in-sync before recovery, keep the replacement in the trio and mark the recovered node as spare capacity for future assignments.

## Acceptance criteria

- [ ] Unhealthy node beyond threshold enqueues rebalance/backfill jobs for all affected repositories
- [ ] Explicit storage node deregistration triggers the same rebalance pipeline
- [ ] Replacement node selected via Replica Set Planner excluding dead node
- [ ] Backfill from current primary populates replacement copy via peer mTLS sync
- [ ] Repository never marked `RF3Healthy` while fewer than two copies match primary watermark
- [ ] Recovered node before replacement in-sync: original trio restored, replacement assignment cancelled
- [ ] Recovered node after replacement in-sync: replacement kept, recovered node marked spare
- [ ] Handler/worker tests cover unhealthy trigger, deregistration trigger, reattach, and spare-capacity paths
- [ ] Integration test simulates node stop/start and verifies correct trio membership outcome

## Blocked by

- [06-primary-failover-and-epoch-promotion.md](./06-primary-failover-and-epoch-promotion.md)
- [08-rf1-to-rf3-background-backfill.md](./08-rf1-to-rf3-background-backfill.md)

## User stories covered

- 24 — As an operator adding storage capacity, I want new nodes to participate in replica-set assignment for new repositories without manual per-repo configuration, so that scaling storage remains self-registration driven.
- 25 — As an operator, I want unhealthy nodes automatically replaced in affected replica sets, so that a dead node does not leave repositories permanently at RF=2 without human action.
- 26 — As an operator, I want explicit node deregistration to trigger the same rebalance pipeline as heartbeat failure, so that graceful and ungraceful removal behave consistently.
- 27 — As the system, I want a recovered node reattached to its original replica sets when replacement backfill has not yet reached in-sync status, so that transient outages do not permanently reshuffle trios.
- 28 — As the system, I want a recovered node to enter spare capacity when replacement backfill already reached in-sync status, so that flip-flopping is avoided after meaningful recovery work completed.
- 29 — As the system, I want rebalance to never treat a repository as safe with fewer than two durable copies, so that automatic healing does not sacrifice the data-loss bar.

## Notes

- Coordinates with promotion (slice 06): unhealthy primary may be promoted first, then replacement fills the evicted slot.
- Reuses backfill machinery from slice 08 where possible.
