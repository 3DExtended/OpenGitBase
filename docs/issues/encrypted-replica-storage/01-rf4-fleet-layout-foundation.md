<!-- forge: #119 -->

# RF=4 fleet layout foundation

## Metadata

- ID: ers-01
- Type: AFK
- Status: ready
- Source: docs/prd/encrypted-replica-storage.md

## Parent

[PRD: Encrypted Replica Storage](../../prd/encrypted-replica-storage.md)

## What to build

Prepare the local and deployment fleet topology for four-copy replication on the existing three-node platform fleet. Primary and read replica colocate on one storage node; encrypted replicas occupy the other two nodes. Update compose, bootstrap, and documentation so developers exercise RF=4 placement constraints without adding a fourth storage service.

Verify enrollment, heartbeat, and internal routing still work when a single node holds two plaintext roles and other nodes hold artifact-only storage paths.

## Acceptance criteria

- [ ] Default compose stack documents RF=4 role mapping: storage-1 = primary+read, storage-2 and storage-3 = encrypted replicas
- [ ] Bootstrap and fleet enrollment scripts succeed with unchanged three-node topology
- [ ] Storage agents report heartbeats from all three nodes after bootstrap
- [ ] README or ops docs describe minimum fleet size and colocation rules for RF=4
- [ ] Smoke test or compose health check confirms three storage nodes register as healthy

## Blocked by

- None — can start immediately

## User stories covered

- 55 — As a developer running locally, I want the default three-node compose fleet to exercise four-copy replication with primary+read colocated on one node and encrypted copies on the other two, so that local behavior matches production invariants.
- 56 — As a developer, I want repository creation to fail with a clear error when insufficient healthy nodes exist for four-copy placement, so that local behavior matches production constraints.

## Notes

This slice is infrastructure-only; schema and create logic land in later issues. Goal is a stable fleet baseline before RF=4 provisioning changes.
