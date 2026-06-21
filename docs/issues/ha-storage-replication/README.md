# HA storage replication — implementation issues

Vertical slices for [PRD: HA Storage Replication (RF=3)](../../prd/ha-storage-replication.md).

Implement in dependency order; each issue lists explicit blockers.

| ID | Issue | Type | Status | Blocked by |
|----|-------|------|--------|------------|
| 01 | [Three-node fleet foundation](./01-three-node-fleet-foundation.md) | AFK | ready | — |
| 02 | [Replica set schema and quorum create](./02-replica-set-schema-and-quorum-create.md) | AFK | ready | 01 |
| 03 | [Storage peer mTLS replication](./03-storage-peer-mtls-replication.md) | AFK | ready | 02 |
| 04 | [Quorum push and watermark commit](./04-quorum-push-and-watermark-commit.md) | AFK | ready | 03 |
| 05 | [Read/write routing (access check + dispatcher)](./05-read-write-routing.md) | AFK | ready | 04 |
| 06 | [Primary failover and epoch promotion](./06-primary-failover-and-epoch-promotion.md) | AFK | ready | 05 |
| 07 | [Quorum delete and async third scrub](./07-quorum-delete-and-async-third-scrub.md) | AFK | ready | 02 |
| 08 | [RF=1 → RF=3 background backfill](./08-rf1-to-rf3-background-backfill.md) | AFK | ready | 02, 03 |
| 09 | [Automatic rebalance and reattach](./09-automatic-rebalance-and-reattach.md) | AFK | ready | 06, 08 |
| 10 | [Anti-entropy reconciler](./10-anti-entropy-reconciler.md) | AFK | ready | 07, 09 |
| 11 | [Admin UI replication status](./11-admin-ui-replication-status.md) | AFK | ready | 06 |
| 12 | [End-to-end HA integration tests](./12-end-to-end-ha-integration-tests.md) | AFK | ready | 10, 11 |

## Dependency graph

```
01 → 02 → 03 → 04 → 05 → 06 → 09 → 10 → 12
        ↘ 07 ────────────────↗
        ↘ 08 → 09
              06 → 11
```

## Source

[docs/prd/ha-storage-replication.md](../../prd/ha-storage-replication.md)
