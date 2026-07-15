<!-- forge: #126 -->

# RF=3 to RF=4 background backfill

## Metadata

- ID: ers-08
- Type: AFK
- Status: ready
- Source: docs/prd/encrypted-replica-storage.md

## Parent

[PRD: Encrypted Replica Storage](../../prd/encrypted-replica-storage.md)

## What to build

Background migration service that upgrades existing RF=3 repositories to the four-copy model without user-visible downtime.

Per repository in `Rf3Healthy`:

1. Designate read replica from highest-watermark non-primary copy
2. Assign two encrypted replica slots on platform nodes
3. Generate Repository Key if not present
4. Produce initial encrypted bundle from current primary
5. Transition `ReplicationState` through `Rf4Migrating` → `Rf4Healthy`

Git operations continue on existing copies during migration. Follow the same background service pattern as RF=1→RF=3 backfill. Register in HA background service loop.

## Acceptance criteria

- [ ] Backfill service processes RF=3 repos incrementally without blocking API requests
- [ ] Migration assigns read replica role and two encrypted slots correctly
- [ ] Initial encrypted artifact produced and confirmed before marking `Rf4Healthy`
- [ ] Push/quorum on partially migrated repos continues via RF=3 path until migration completes
- [ ] Admin replication state shows `Rf4Migrating` during transition
- [ ] Handler/service tests cover happy migration, mid-migration push, and failure retry
- [ ] No repository left permanently in `Rf4Migrating` after transient errors (retry with backoff)

## Blocked by

- [05-encrypted-quorum-push.md](./05-encrypted-quorum-push.md)

## User stories covered

- 51 — As an operator upgrading from RF=3, I want existing repositories migrated to the four-copy model via background backfill, so that git keeps working during migration.
- 52 — As a repository owner, I want no user-visible downtime during migration from RF=3 to RF=4, so that upgrades are transparent.

## Notes

Can run in parallel with issues 06–07 once encrypted push (05) exists. Does not require recovery (07) to start.
