# RF=1 → RF=3 background backfill

## Metadata

- ID: ha-storage-08
- Type: AFK
- Status: ready
- Source: docs/prd/ha-storage-replication.md

## Parent

[PRD: HA Storage Replication (RF=3)](../../prd/ha-storage-replication.md)

## What to build

Migrate existing single-node repositories (pre-HA `StorageNodeId`-only assignment) to RF=3 via a background backfill worker. For each unmigrated repo: mark `ReplicationState = RF1Backfilling`, plan two additional replicas via the Replica Set Planner, provision bare repos on new nodes, git-sync from the existing primary using peer mTLS replication, and insert `RepositoryReplica` rows. When all three watermarks match, mark `RF3Healthy`.

Git operations continue on the original primary during backfill. New repositories use the slice 02 path and do not enter this worker.

## Acceptance criteria

- [ ] Backfill worker identifies repositories without a full RF=3 replica set
- [ ] Affected repos transition through `RF1Backfilling` to `RF3Healthy` state
- [ ] Two additional replicas are provisioned and populated via git-native sync from existing primary
- [ ] Git clone/fetch/push against original primary continues during backfill
- [ ] After backfill completes, repository has three `RepositoryReplica` rows with matching watermarks
- [ ] Backfill failure marks repo degraded and retries without data loss on existing primary copy
- [ ] Handler/worker tests cover happy path and partial failure retry
- [ ] Integration test seeds RF=1 repo and verifies RF=3 completion

## Blocked by

- [02-replica-set-schema-and-quorum-create.md](./02-replica-set-schema-and-quorum-create.md)
- [03-storage-peer-mtls-replication.md](./03-storage-peer-mtls-replication.md)

## User stories covered

- 30 — As an operator upgrading from single-node assignment, I want existing repositories backfilled to RF=3 in the background from their current primary, so that git keeps working during migration.

## Notes

- Writes during backfill: enabled once quorum path exists on new trio members per PRD assumption; document chosen policy in implementation.
- Admin visibility of backfill state lands in slice 11.
