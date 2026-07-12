# Org storage self-service UI — work items

Branch strategy: **main** (sequential commits on default branch).

**Source PRD:** [docs/prd/org-storage-self-service-ui.md](../../../docs/prd/org-storage-self-service-ui.md)

Completes the operator-facing gap in **ers-14** (enrollment UI, capacity PATCH, bootstrap script, storage docs).

## Work items

| Order | ID | Title | Type | Status | Blocked by |
|-------|-----|-------|------|--------|------------|
| 1 | [oss-01](./items/oss-01.md) | Org storage node capacity API | AFK | ready | — |
| 2 | [oss-02](./items/oss-02.md) | Org storage bootstrap script | AFK | ready | — |
| 3 | [oss-03](./items/oss-03.md) | Org storage self-service UI | AFK | ready | oss-01, oss-02 |
| 4 | [oss-04](./items/oss-04.md) | Storage operator documentation | AFK | ready | oss-02, oss-03 |
| 5 | [oss-05](./items/oss-05.md) | Org storage visual regression | AFK | ready | oss-03 |

## Dependency graph

```
oss-01 ──┐
         ├──► oss-03 ──► oss-04
oss-02 ──┘              └──► oss-05
```

## Execution order

1. **oss-01** and **oss-02** may proceed in parallel.
2. **oss-03** after both (UI needs capacity PATCH + real bootstrap snippet).
3. **oss-04** after UI and script (docs describe implemented behavior).
4. **oss-05** after UI components exist for gallery fixtures.

## Verification (each item)

- **oss-01:** `dotnet test` (org storage capacity controller/handler tests)
- **oss-02:** manual or scripted smoke on clean Linux VM with Docker (document in item notes)
- **oss-03:** `pnpm test` (web component/API client tests)
- **oss-04:** docs route smoke / link check
- **oss-05:** `pnpm test:visual` (org storage gallery snapshots)

## Out of scope (deferred)

- [ers-17](../../../docs/issues/encrypted-replica-storage/17-org-node-capacity-shrink-via-rebalance.md) — shrink below used bytes via platform rebalance
