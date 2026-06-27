# PRD issues TDD (main) — Merge Requests

Source: [docs/prd/merge-requests.md](../../docs/prd/merge-requests.md)  
Work items: [docs/issues/merge-requests/README.md](../../docs/issues/merge-requests/README.md)

Branch strategy: **main** (all work items committed sequentially on default branch).

## External prerequisites (pre-existing)

| Dependency | Status |
|------------|--------|
| Git storage proxy | Implemented |
| disc-04 Thread comments | Implemented |
| disc-07 In-app notifications | Implemented |
| disc-08 Email notifications | Implemented |

## Execution order

| Order | ID | Title | Status | Blocked by |
|------:|-----|-------|--------|------------|
| 1 | mr-01 | Merge request authorization | completed | — |
| 2 | mr-02 | Default branch persistence and settings | pending | — |
| 3 | mr-03 | Protected branch and push rule CRUD | pending | mr-02 |
| 4 | mr-04 | Git push enforcement | pending | mr-03 |
| 5 | mr-05 | Storage diff, mergeability, and merge execute | pending | git storage proxy |
| 6 | mr-06 | Merge request core (API + list, create, detail shell) | pending | mr-01, mr-02 |
| 7 | mr-07 | Approvals and merge gates | pending | mr-06 |
| 8 | mr-08 | Server-side merge and discussion closes links | pending | mr-04, mr-05, mr-07 |
| 9 | mr-09 | Shared collaboration UI components | pending | disc-04 |
| 10 | mr-10 | Overview comments | pending | mr-06, mr-09 |
| 11 | mr-11 | Changes tab, diff, and review threads | pending | mr-05, mr-10 |
| 12 | mr-12 | Branches and push rules settings UI | pending | mr-03, mr-06 |
| 13 | mr-13 | Post-push create banner | pending | mr-02, mr-06 |
| 14 | mr-14 | Merge request notifications | pending | mr-06, disc-07, disc-08 |
| 15 | mr-15 | Linked discussions sidebar | pending | mr-06, mr-08 |
| 16 | mr-16 | End-to-end merge request integration tests | pending | mr-08, mr-11, mr-14 |

**First demo milestone:** mr-08 (protect → push → MR → approve → merge).

## Dependency graph

```
mr-01 ──┐
mr-02 ──┼──► mr-06 ──► mr-07 ──► mr-08 ──► mr-16
        │       │                      ▲
mr-03 ──┼──► mr-04 ───────────────────┤
        │       │                      │
        ├──► mr-12    mr-05 ──► mr-11 ─┘
        └──► mr-13

disc-04 ──► mr-09 ──► mr-10 ──► mr-11

mr-06 + disc-07/08 ──► mr-14 ──► mr-16
mr-06 + mr-08 ──► mr-15
```
