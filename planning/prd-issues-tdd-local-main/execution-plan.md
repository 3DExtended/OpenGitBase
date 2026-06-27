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
| 2 | mr-02 | Default branch persistence and settings | completed | 79a86ed |
| 3 | mr-03 | Protected branch and push rule CRUD | completed | 23d5a3d |
| 4 | mr-04 | Git push enforcement | completed | — |
| 5 | mr-05 | Storage diff, mergeability, and merge execute | completed | — |
| 6 | mr-06 | Merge request core (API + list, create, detail shell) | completed | — |
| 7 | mr-07 | Approvals and merge gates | completed | — |
| 8 | mr-08 | Server-side merge and discussion closes links | completed | — |
| 9 | mr-09 | Shared collaboration UI components | completed | — |
| 10 | mr-10 | Overview comments | completed | — |
| 11 | mr-11 | Changes tab, diff, and review threads | completed | — |
| 12 | mr-12 | Branches and push rules settings UI | completed | — |
| 13 | mr-13 | Post-push create banner | completed | — |
| 14 | mr-14 | Merge request notifications | completed | — |
| 15 | mr-15 | Linked discussions sidebar | completed | — |
| 16 | mr-16 | End-to-end merge request integration tests | completed | — |

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
