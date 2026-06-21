# Execution Plan — HA Storage Replication (RF=3)

**PRD:** `docs/prd/ha-storage-replication.md`  
**Items:** `docs/issues/ha-storage-replication/`  
**Strategy:** Sequential TDD on `main`, sensible commits per work item  
**Default branch:** `main`

## Topological execution order

| Step | ID | Title | Status |
|------|-----|-------|--------|
| 1 | ha-storage-01 | Three-node fleet foundation | in_progress |
| 2 | ha-storage-02 | Replica set schema and quorum create | pending |
| 3 | ha-storage-03 | Storage peer mTLS replication | pending |
| 4 | ha-storage-04 | Quorum push and watermark commit | pending |
| 5 | ha-storage-05 | Read/write routing | pending |
| 6 | ha-storage-06 | Primary failover and epoch promotion | pending |
| 7 | ha-storage-07 | Quorum delete and async third scrub | pending |
| 8 | ha-storage-08 | RF=1 → RF=3 background backfill | pending |
| 9 | ha-storage-09 | Automatic rebalance and reattach | pending |
| 10 | ha-storage-10 | Anti-entropy reconciler | pending |
| 11 | ha-storage-11 | Admin UI replication status | pending |
| 12 | ha-storage-12 | End-to-end HA integration tests | pending |

## Dependency graph

```
01 → 02 → 03 → 04 → 05 → 06 → 09 → 10 → 12
        ↘ 07 ────────────────↗
        ↘ 08 → 09
              06 → 11
```
