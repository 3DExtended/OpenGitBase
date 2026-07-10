# Phase 2 execution plan

Branch strategy: **main** (all work items committed sequentially on default branch).

## Work items

| Order | ID | Title | Status |
|-------|-----|-------|--------|
| 1 | ers-12 | Org storage node registration and capacity | complete |
| 2 | ers-13 | Self-host tier placement | complete |
| 3 | ers-14 | Org quota credits and placement settings UI | complete |

## Dependency graph

```
ers-11 -> ers-12 -> ers-13
                 \-> ers-14
```

## Notes

Phase 3 (ers-15, ers-16) is out of scope for this run.
