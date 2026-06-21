# Execution Plan — HA Storage Replication (RF=3)

**PRD:** `docs/prd/ha-storage-replication.md`  
**Items:** `docs/issues/ha-storage-replication/`  
**Strategy:** Sequential TDD on `main`, sensible commits per work item  
**Default branch:** `main`

## Topological execution order

| Step | ID | Title | Status |
|------|-----|-------|--------|
| 1 | ha-storage-01 | Three-node fleet foundation | completed |
| 2 | ha-storage-02 | Replica set schema and quorum create | completed |
| 3 | ha-storage-03 | Storage peer mTLS replication | completed |
| 4 | ha-storage-04 | Quorum push and watermark commit | completed |
| 5 | ha-storage-05 | Read/write routing | completed |
| 6 | ha-storage-06 | Primary failover and epoch promotion | completed |
| 7 | ha-storage-07 | Quorum delete and async third scrub | completed |
| 8 | ha-storage-08 | RF=1 → RF=3 background backfill | completed |
| 9 | ha-storage-09 | Automatic rebalance and reattach | completed |
| 10 | ha-storage-10 | Anti-entropy reconciler | completed |
| 11 | ha-storage-11 | Admin UI replication status | completed |
| 12 | ha-storage-12 | End-to-end HA integration tests | completed |

## Dependency graph

```
01 → 02 → 03 → 04 → 05 → 06 → 09 → 10 → 12
        ↘ 07 ────────────────↗
        ↘ 08 → 09
              06 → 11
```

## Commits (main)

| Item | SHA |
|------|-----|
| 01 | c96dd65 |
| 02 | 4c6333d |
| 03 | 643b1a0 |
| 04 | 7c95f88 |
| 05 | f990008 |
| 06 | 9081762 |
| 07 | bdc5b07 |
| 08 | 0c9afdc |
| 09 | a212a7d |
| 10 | d9558ed |
| 11 | 81d7964 |
| 12 | 84b7ce9 |
