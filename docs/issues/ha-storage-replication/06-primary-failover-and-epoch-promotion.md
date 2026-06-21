# Primary failover and epoch promotion

## Metadata

- ID: ha-storage-06
- Type: AFK
- Status: ready
- Source: docs/prd/ha-storage-replication.md

## Parent

[PRD: HA Storage Replication (RF=3)](../../prd/ha-storage-replication.md)

## What to build

When a repository's primary storage node becomes unhealthy (missed heartbeat threshold), the API automatically promotes the replica with the highest `AppliedWatermark` to primary, increments `ReplicationEpoch`, and updates replica roles in a transactional DB operation. Dispatchers pick up the new primary on the next access check — no dispatcher-local failover state. Stale primaries attempting watermark commits after promotion are rejected.

Enqueue a rebalance job for the evicted node slot (handled fully in slice 09). This slice focuses on promotion correctness and git ops resuming against the new primary.

## Acceptance criteria

- [ ] Primary unhealthy beyond configured threshold triggers promotion for affected repositories
- [ ] Promotion selects replica with highest `AppliedWatermark`; tie-break deterministically (e.g. node id)
- [ ] `ReplicationEpoch` increments atomically on promotion; old primary role updated
- [ ] Access-check returns new primary after promotion without dispatcher restart
- [ ] Watermark commit from pre-promotion primary (stale epoch) is rejected
- [ ] Clone/fetch succeeds via new primary or in-sync replicas after promotion
- [ ] Push succeeds via new primary when quorum 2/3 is reachable after promotion
- [ ] Handler/worker tests cover promotion selection, epoch bump, and stale-epoch rejection
- [ ] Integration test simulates primary node failure and verifies push/clone resume

## Blocked by

- [05-read-write-routing.md](./05-read-write-routing.md)

## User stories covered

- 18 — As the system, I want the API to automatically promote the most up-to-date replica when the primary becomes unhealthy, so that git operations can resume without operator action.
- 19 — As the system, I want each repository to carry a monotonic epoch/generation counter, so that split-brain writes from a stale primary are rejected after promotion.
- 20 — As a dispatcher, I want routing to update after promotion via the normal access-check path, so that failover does not require dispatcher-local state.
- 21 — As a git client, I want clone/fetch to resume from a promoted primary or in-sync replica after failover, so that read availability recovers automatically.
- 22 — As a git client, I want push to resume once a new primary is elected and quorum is reachable, so that write availability recovers without manual intervention when two nodes remain healthy.

## Notes

- Replacement node backfill for the dead slot is slice 09; this slice may enqueue jobs but need not complete rebalance.
- Brief promotion window where push fails is acceptable; clients retry.
