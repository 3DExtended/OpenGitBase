# Phase 3 execution plan

Branch strategy: **main** (all work items committed sequentially on default branch).

## Work items

| Order | ID | Title | Status |
|-------|-----|-------|--------|
| 1 | ers-15 | Cross-org encrypted placement algorithm | complete |
| 2 | ers-16 | Per-repo byte overrides and capacity engine | complete |

## Dependency graph

```
ers-13 -> ers-15 -> ers-16
ers-14 -/
```

## Execution order

1. **ers-15** — `EncryptedReplicaPlacementEngine`, planner integration, unit + integration tests
2. **ers-16** — `MaxBytesOverride` schema/API/enforcement, eligibility UI, visual snapshots

## Verification (each item)

- `dotnet test` (scoped to touched projects)
- Compose E2E where API/storage touched (`./scripts/test-ha-storage-e2e.sh`)
- `pnpm test:visual` for ers-16 UI work
