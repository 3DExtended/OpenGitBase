# Execution plan — `ogb mr`

**PRD:** `docs/prd/ogb-cli-mr.md`  
**Work items:** `planning/ogb-cli-mr/index.md`

Branch strategy: **main** (all work items committed sequentially on default branch).

## Dependency graph

```
mr-01 ──┬──► mr-02
        ├──► mr-03
        ├──► mr-04
        ├──► mr-05 ──┐
        ├──► mr-06   │
        ├──► mr-07   ├──► mr-11 ──► mr-12
        ├──► mr-08   │
        ├──► mr-09   │
        └──► mr-10 ──┘
```

## Execution order

| # | ID | Title | Status |
|---|-----|-------|--------|
| 1 | mr-01 | MR API client and `mr list` | completed |
| 2 | mr-02 | `ogb mr view` | completed |
| 3 | mr-03 | `ogb mr status` | completed |
| 4 | mr-04 | `ogb mr diff` | completed |
| 5 | mr-05 | Git branch resolver and `mr create` | completed |
| 6 | mr-06 | `ogb mr close` | completed |
| 7 | mr-07 | `ogb mr ready` | completed |
| 8 | mr-08 | `ogb mr edit` | completed |
| 9 | mr-09 | `ogb mr approve` | completed |
| 10 | mr-10 | `ogb mr merge` | completed |
| 11 | mr-11 | MR integration tests | completed |
| 12 | mr-12 | Compose E2E smoke | completed (test added; compose run skipped — Docker unavailable) |
