## Summary

Added `EncryptedReplicaPlacementEngine` scoring cross-org community nodes ahead of platform nodes for encrypted replica slots. Integrated into `ReplicaSetPlanner` for tier-0 and external encrypted slots in tiers 1–2. Capacity and repository-count penalties applied.

## Linked Context

- PRD: `docs/prd/encrypted-replica-storage.md`
- Work item: `ers-15`

## Dependency Graph

### Direct dependencies (blocked by)

- ers-13, ers-14

### Full chain

`ers-11 -> ers-12 -> ers-13/14 -> ers-15`

## Status

- Branch: `main`
- Tests: `dotnet test --filter FullyQualifiedName~EncryptedReplica|FullyQualifiedName~ReplicaSetPlanner|FullyQualifiedName~CrossOrg` — 25 passed
- Visual snapshots: none
- Commit(s): pending
