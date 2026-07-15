<!-- forge: #159 -->

# Admin UI replication status

## Metadata

- ID: ha-storage-11
- Type: AFK
- Status: ready
- Source: docs/prd/ha-storage-replication.md

## Parent

[PRD: HA Storage Replication (RF=3)](../../prd/ha-storage-replication.md)

## What to build

Expose replication health to operators via admin API and web UI. Per repository: primary node, replica nodes, primary and applied watermarks, in-sync status per replica, replication state enum (`RF3Healthy`, `RF1Backfilling`, `Degraded`, `Promoting`), and active backfill/rebalance jobs. Per storage node: count of repos where node is primary or replica, spare-capacity indicator when applicable.

Extend existing admin storage surfaces rather than building a separate dashboard from scratch.

## Acceptance criteria

- [ ] Admin API returns per-repository replication detail (primary, replicas, watermarks, state, active jobs)
- [ ] Admin API returns per-node replication summary (role counts, spare status)
- [ ] Admin storage UI displays replication state for the fleet and/or per-repo detail
- [ ] UI distinguishes healthy RF=3, backfilling, degraded, and promoting states
- [ ] Watermark lag visible when a replica is not in-sync
- [ ] API tests cover replication status endpoints with seeded replica sets
- [ ] UI renders without error when viewing a pre-backfill RF=1 repository

## Blocked by

- [06-primary-failover-and-epoch-promotion.md](./06-primary-failover-and-epoch-promotion.md)

## User stories covered

- 31 — As an operator, I want per-repository replication state visible in the admin UI (for example: backfilling, RF=3 healthy, degraded, promoting), so that fleet health is auditable without reading logs.
- 33 — As the API, I want to expose replication health on repository admin/detail surfaces, so that operators can see when a repo is below RF=3 or missing quorum.

## Notes

- Slice 32 (heartbeat watermarks) is delivered in slice 04; this slice consumes that data for display.
- Can ship incrementally: fleet-level on storage admin page first, repo detail second.
