# Quorum delete and async third scrub

## Metadata

- ID: ha-storage-07
- Type: AFK
- Status: ready
- Source: docs/prd/ha-storage-replication.md

## Parent

[PRD: HA Storage Replication (RF=3)](../../prd/ha-storage-replication.md)

## What to build

Extend repository deletion for RF=3 repositories: delete bare repos on at least **two of three** trio members via the storage internal HTTP API before removing the database row. If one node is unreachable, deletion still succeeds when quorum is met. Enqueue async scrub of the third node's copy after DB removal. Roll back or fail safely if quorum delete cannot be achieved.

This slice can proceed in parallel with push/failover slices (04–06) once quorum create (02) exists.

## Acceptance criteria

- [ ] Repository delete removes bare repos on at least two trio members before DB row is deleted
- [ ] Delete succeeds when one storage node is down but two others confirm removal
- [ ] Delete fails without DB removal when fewer than two nodes confirm deletion
- [ ] Third node's copy is scrubbed asynchronously after successful quorum delete and DB removal
- [ ] `RepositoryReplica` rows and replication metadata removed with the repository DB row
- [ ] Handler tests cover happy path, one-node-down success, and quorum failure
- [ ] Integration test confirms bare repo absent on two nodes and eventually absent on third after async scrub

## Blocked by

- [02-replica-set-schema-and-quorum-create.md](./02-replica-set-schema-and-quorum-create.md)

## User stories covered

- 10 — As a repository owner deleting a repository, I want deletion confirmed on at least two nodes before the database record is removed, so that deleted repositories do not leave authoritative orphans while still allowing delete when one node is down.
- 11 — As the system, I want the third node's copy removed asynchronously after a quorum delete, so that delete succeeds under the same 2/3 policy as push.

## Notes

- Orphan detection for failed async scrubs is handled by slice 10 (reconciler).
- Pre-HA single-node delete path should remain functional until backfill completes or be unified if all repos are RF=3.
