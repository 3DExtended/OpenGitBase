<!-- forge: #127 -->

# Delete, rebalance, and anti-entropy extensions

## Metadata

- ID: ers-09
- Type: AFK
- Status: ready
- Source: docs/prd/encrypted-replica-storage.md

## Parent

[PRD: Encrypted Replica Storage](../../prd/encrypted-replica-storage.md)

## What to build

Extend existing delete, rebalance, and anti-entropy services for four-copy layout.

**Delete quorum:** confirm deletion on primary, read replica, and encrypted artifact stores before DB removal; async scrub for failed nodes (extend existing 2/3+ policy to four-copy semantics).

**Rebalance:** replace unhealthy encrypted replicas and read replicas; never treat repo safe with fewer than one encrypted copy and one plaintext copy.

**Anti-entropy:** detect and repair lagging read replica git sync; missing or stale encrypted artifacts; orphaned artifact directories.

Preserve reattach semantics for recovered nodes where applicable.

## Acceptance criteria

- [ ] Repository delete removes bare git from primary and read replica and artifacts from both encrypted nodes
- [ ] Delete succeeds under degraded four-copy with same durability bar as today (minimum confirmed deletes before DB row removal)
- [ ] Unhealthy encrypted replica triggers replacement provisioning and artifact backfill
- [ ] Anti-entropy reconciler repairs read replica lag and missing encrypted artifacts
- [ ] Rebalance never marks repository healthy with zero encrypted copies
- [ ] Service tests cover encrypted node loss, read replica lag repair, and delete with one node down
- [ ] Heartbeat watermark and artifact watermark reporting integrated for lag detection

## Blocked by

- [05-encrypted-quorum-push.md](./05-encrypted-quorum-push.md)
- [07-hot-promotion-and-cold-recovery.md](./07-hot-promotion-and-cold-recovery.md)

## User stories covered

- 45 — As an operator, I want unhealthy encrypted replicas replaced automatically, so that RF=4 durability is maintained without manual intervention.
- 46 — As an operator, I want a periodic reconciler to repair lagging read replicas and missing encrypted artifacts, so that silent drift is corrected.
- 48 — As a storage node, I want to report applied watermarks and artifact watermarks in heartbeat payloads, so that the API can detect lag and in-sync status.
- 49 — As a platform operator, I want replication failures to continue participating in quorum calculations and attention signals, so that existing observability semantics are preserved.
- 50 — As a platform operator, I want background reconciliation jobs to operate with encrypted replication artifacts, so that anti-entropy extends to the new model.

## Notes

Depends on recovery (07) so rebalance interacts correctly with `Recovering` state. Delete artifact endpoint from issue 03 wired here.
