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
| 1 | ers-01 | RF=4 fleet layout foundation | pending |
| 2 | ers-02 | RF=4 schema, repository keys, and artifact library | pending |
| 3 | ers-03 | Storage artifact API and encrypted node isolation | pending |
| 4 | ers-04 | Four-copy repository create | pending |
| 5 | ers-05 | Encrypted quorum push | pending |
| 6 | ers-06 | Read/write routing split | pending |
| 7 | ers-07 | Hot promotion and cold recovery | pending |
| 8 | ers-08 | RF=3 to RF=4 background backfill | pending |
| 9 | ers-09 | Delete, rebalance, and anti-entropy extensions | pending |
| 10 | ers-10 | Admin UI four-copy replication status | pending |
| 11 | ers-11 | Phase 1 E2E and integration tests | pending |

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
| ers-01 | |
| ers-02 | |
| ers-03 | |
| ers-04 | |
| ers-05 | |
| ers-06 | |
| ers-07 | |
| ers-08 | |
| ers-09 | |
| ers-10 | |
| ers-11 | |
