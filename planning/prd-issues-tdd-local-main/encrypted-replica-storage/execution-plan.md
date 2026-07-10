# Execution Plan — Encrypted Replica Storage (Phase 1)

**PRD:** `docs/prd/encrypted-replica-storage.md`  
**Items:** `docs/issues/encrypted-replica-storage/` (01–11)  
**Strategy:** Sequential TDD on `main`, sensible commits per work item  
**Branch strategy:** **main** (all work items committed sequentially on default branch)

## Scope

Phase 1 only — platform four-copy encryption through E2E tests. Phase 2/3 (issues 12–16) excluded.

## Topological execution order

| Step | ID | Title | Status |
|------|-----|-------|--------|
| 1 | ers-01 | RF=4 fleet layout foundation | completed |
| 2 | ers-02 | RF=4 schema, repository keys, and artifact library | completed |
| 3 | ers-03 | Storage artifact API and encrypted node isolation | completed |
| 4 | ers-04 | Four-copy repository create | completed |
| 5 | ers-05 | Encrypted quorum push | completed |
| 6 | ers-06 | Read/write routing split | completed |
| 7 | ers-07 | Hot promotion and cold recovery | completed |
| 8 | ers-08 | RF=3 to RF=4 background backfill | completed |
| 9 | ers-09 | Delete, rebalance, and anti-entropy extensions | completed |
| 10 | ers-10 | Admin UI four-copy replication status | completed |
| 11 | ers-11 | Phase 1 E2E and integration tests | completed |

## Dependency graph

```
01 ─┐
02 → 03 → 04 → 05 → 07 → 09 → 11
         ↘ 06 ↗    ↘ 08
              07 → 10
```

## Commits (main)

| Item | SHA |
|------|-----|
| ers-01 | 8e72101 |
| ers-02 | edc7b41 |
| ers-03 | 0bbbe80 |
| ers-04 | 68af814 |
| infra | e36c65f |
| ers-05–11 | db9f4c7 |
