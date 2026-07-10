# Four-copy repository create

## Metadata

- ID: ers-04
- Type: AFK
- Status: ready
- Source: docs/prd/encrypted-replica-storage.md

## Parent

[PRD: Encrypted Replica Storage](../../prd/encrypted-replica-storage.md)

## What to build

Wire four-copy replication into repository creation end to end on the platform fleet.

Extend Replica Set Planner to assign four roles from healthy platform nodes:

- Primary (bare git)
- Read replica (bare git; may colocate with primary)
- Two encrypted replicas (artifact directories on distinct nodes)

On create:

1. Select four placements satisfying colocation rules
2. Provision bare git on primary and read-replica targets
3. Provision artifact storage roots on encrypted targets
4. Generate Repository Key via Repository Key Service
5. Persist four RepositoryReplica rows, replication metadata, and key record
6. Roll back all provisions on any failure

Reject creation when fewer than three healthy distinct nodes exist (minimum for primary+read colocated + two encrypted on separate nodes). Return clear error messaging.

## Acceptance criteria

- [ ] Replica Set Planner unit tests cover four-role assignment, colocation rules, and insufficient fleet rejection
- [ ] Repository create provisions primary, read replica, and two encrypted slots before DB commit
- [ ] Repository create generates and stores envelope-encrypted Repository Key
- [ ] Failed create rolls back all provisioned bare repos and artifact directories
- [ ] Created repository has four RepositoryReplica rows with correct roles at watermark 0
- [ ] Handler tests cover happy path, provision failure rollback, and insufficient node rejection
- [ ] Compose smoke test confirms four-copy layout on default three-node fleet after create

## Blocked by

- [01-rf4-fleet-layout-foundation.md](./01-rf4-fleet-layout-foundation.md)
- [03-storage-artifact-api-and-encrypted-node-isolation.md](./03-storage-artifact-api-and-encrypted-node-isolation.md)

## User stories covered

- 8 — (adapted) As a repository owner creating a repository, I want all four copies provisioned before the API returns success, so that a new repository never exists as a single copy even briefly.
- 9 — (adapted) As the API, I want new repository creation blocked when insufficient healthy storage nodes exist, so that RF=4 is enforced from birth.
- 42 — As the API assigning a new repository, I want storage node selection based on available capacity, repository count, and max bytes per repository, so that placement avoids overcommit.
- 53 — As a developer, I want public and private repositories to use the same replication pipeline, so that the system has one code path to maintain.

## Notes

Phase 1 uses platform nodes only; org-contributed nodes and cross-org placement come in Phase 2/3. Capacity scoring beyond free-bytes ordering can remain simple until issue 16.
