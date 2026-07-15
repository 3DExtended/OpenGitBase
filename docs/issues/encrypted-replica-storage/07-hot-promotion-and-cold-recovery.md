<!-- forge: #125 -->

# Hot promotion and cold recovery

## Metadata

- ID: ers-07
- Type: AFK
- Status: ready
- Source: docs/prd/encrypted-replica-storage.md

## Parent

[PRD: Encrypted Replica Storage](../../prd/encrypted-replica-storage.md)

## What to build

Implement two recovery paths when the write primary fails.

**Hot promotion (read replica in sync):**

- When read replica `AppliedWatermark == PrimaryWatermark`, promote read replica to primary
- Increment replication epoch; reassign read replica role to a new node or mark pending
- Regenerate encrypted artifacts from new primary
- Reuse existing epoch split-brain guards

**Cold recovery (both plaintext copies lost):**

- Set `ReplicationState = Recovering`
- Obtain Repository Key from envelope store
- Select encrypted replica with highest artifact watermark
- Decrypt bundle, reconstruct bare repository on target primary and read nodes
- Verify integrity (bundle verify + ref manifest comparison)
- Assign roles, bump epoch, transition to `Rf4Healthy`
- Audit-log entire workflow; fail safely on integrity errors

Encrypted replicas are never hot-promoted to serve Git.

## Acceptance criteria

- [ ] Primary failure with in-sync read replica completes hot promotion within existing background failover loop
- [ ] Hot promotion increments epoch; stale primary watermark commits rejected
- [ ] Primary + read colocated node failure triggers cold recovery path
- [ ] Cold recovery produces repository with identical refs and reachable objects
- [ ] Integrity verification failure leaves repo in `Recovering` or `Degraded`; writes remain blocked
- [ ] Recovery workflow is auditable (structured log or admin event trail)
- [ ] Integration tests cover hot promote, cold recover, corrupt artifact abort, and interrupted recovery

## Blocked by

- [05-encrypted-quorum-push.md](./05-encrypted-quorum-push.md)
- [06-read-write-routing-split.md](./06-read-write-routing-split.md)

## User stories covered

- 24 — As the system, I want to hot-promote the read replica to primary when it is in sync and the primary becomes unhealthy, so that git operations resume quickly when one plaintext copy survives.
- 25 — As the system, I want cold recovery from encrypted replicas when both plaintext copies are lost, so that durability is preserved even when the primary+read node fails.
- 26 — As an administrator, I want repository recovery procedures to be deterministic and auditable, so that disaster recovery can be reviewed and repeated.
- 27 — As an administrator, I want recovery to verify repository integrity before resuming writes, so that corrupted artifacts cannot produce a writable but incorrect repository.
- 28 — As an administrator, I want recovery to fail safely when integrity verification fails, so that the system does not silently serve corrupted data.
- 29 — As the system, I want epoch guards to reject stale primary writes after promotion or recovery, so that split-brain is prevented.
- 30 — As a git client, I want clone/fetch to resume from the read replica or promoted primary after failover, so that read availability recovers when a plaintext copy exists.
- 31 — As a git client, I want push to resume after recovery completes and encrypted quorum is reachable, so that write availability returns following cold recovery.

## Notes

Brief promotion/recovery windows may fail pushes; clients retry. Cold recovery duration acceptable per PRD availability trade-off.
