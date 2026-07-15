<!-- forge: #128 -->

# Admin UI four-copy replication status

## Metadata

- ID: ers-10
- Type: AFK
- Status: ready
- Source: docs/prd/encrypted-replica-storage.md

## Parent

[PRD: Encrypted Replica Storage](../../prd/encrypted-replica-storage.md)

## What to build

Extend admin replication API and web UI to surface four-copy roles, migration state, and encrypted artifact lag.

Show per repository:

- Four replica rows with role (Primary, ReadReplica, EncryptedReplica ×2)
- Applied watermark and artifact watermark per encrypted replica
- Replication state including `Rf4Migrating`, `Rf4Healthy`, `Recovering`
- Attention signals for missing encrypted quorum, read replica lag, or incomplete migration

Extend existing admin replication list and detail pages rather than building parallel UI.

## Acceptance criteria

- [ ] Admin replication list API returns four-copy role and state fields
- [ ] Admin replication detail shows all four copies with node names and lag indicators
- [ ] `Rf4Migrating` and `Recovering` states visible with human-readable labels
- [ ] Encrypted replica artifact watermark displayed separately from git applied watermark
- [ ] Web UI renders four-copy layout without breaking existing RF=3 display during migration
- [ ] API tests cover list and detail responses for RF=4 healthy and migrating repos

## Blocked by

- [07-hot-promotion-and-cold-recovery.md](./07-hot-promotion-and-cold-recovery.md)

## User stories covered

- 47 — As an operator, I want per-repository replication state visible in the admin UI including four-copy roles and migration progress, so that fleet health is auditable.

## Notes

Builds on existing admin replication UI from HA storage PRD. Can start API layer before UI if needed.
