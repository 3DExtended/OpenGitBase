# Replica set schema and quorum create

## Metadata

- ID: ha-storage-02
- Type: AFK
- Status: ready
- Source: docs/prd/ha-storage-replication.md

## Parent

[PRD: HA Storage Replication (RF=3)](../../prd/ha-storage-replication.md)

## What to build

Introduce the replication control-plane schema and wire it into repository creation end-to end. Add `RepositoryReplica` rows and replication metadata on `Repository` (primary node, epoch, watermark, replication state). Implement the Replica Set Planner: given healthy storage nodes, pick a primary (most free disk) and two distinct replicas (next most free). Extend repository create to provision a bare repo on **all three** nodes synchronously before persisting the DB row; roll back on any failure. Reject new repository creation when fewer than three healthy storage nodes are available.

**Schema (conceptual, from PRD):**

```
Repository {
  ...existing fields...
  PrimaryStorageNodeId: Guid
  ReplicationEpoch: long
  PrimaryWatermark: long
  ReplicationState: enum   // e.g. RF3Healthy | RF1Backfilling | Degraded | Promoting
}

RepositoryReplica {
  RepositoryId: Guid
  StorageNodeId: Guid
  Role: enum                 // Primary | Replica
  AppliedWatermark: long
  IsInSync: bool
  LastSyncedAt: DateTimeOffset?
  BackfillState: enum
}
```

`StorageNodeId` on `Repository` may remain as alias for primary during transition.

## Acceptance criteria

- [ ] Migration adds `RepositoryReplica` and replication fields on `Repository`
- [ ] Replica Set Planner unit tests cover trio selection, distinct nodes, and capacity ordering
- [ ] Repository create provisions bare repos on all three selected nodes before DB insert
- [ ] Repository create rolls back provisioned copies if DB insert or a later provision call fails
- [ ] Repository create returns a clear error when fewer than three healthy storage nodes exist
- [ ] Created repository has three `RepositoryReplica` rows with one primary and two replicas at watermark 0
- [ ] Handler tests cover happy path, provision failure rollback, and `<3` healthy node rejection
- [ ] Storage integration test or compose smoke test confirms bare repo directories exist on all three nodes after create

## Blocked by

- [01-three-node-fleet-foundation.md](./01-three-node-fleet-foundation.md)

## User stories covered

- 8 — As a repository owner creating a repository, I want bare repos provisioned on all three nodes before the API returns success, so that a new repository never exists as a single copy even briefly.
- 9 — As the API, I want new repository creation blocked when fewer than three storage nodes are healthy, so that RF=3 is enforced from birth in all environments including local development.
- 23 — As the API assigning a new repository, I want the primary chosen as the healthy node with the most free disk space and replicas chosen as the next two distinct healthy nodes by the same signal, so that capacity-weighted spread extends the existing selection logic to RF=3.

## Notes

- Push replication, watermark bumps on write, and read routing are out of scope — delivered in later slices.
- Existing single-node repositories are untouched until slice 08 (backfill).
