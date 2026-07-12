# Encrypted replica storage — implementation issues

Vertical slices for [PRD: Encrypted Replica Storage](../../prd/encrypted-replica-storage.md).

Implement in dependency order; each issue lists explicit blockers.

| ID | Issue | Type | Status | Blocked by |
|----|-------|------|--------|------------|
| 01 | [RF=4 fleet layout foundation](./01-rf4-fleet-layout-foundation.md) | AFK | ready | — |
| 02 | [RF=4 schema, repository keys, and artifact library](./02-rf4-schema-keys-and-artifact-library.md) | AFK | ready | — |
| 03 | [Storage artifact API and encrypted node isolation](./03-storage-artifact-api-and-encrypted-node-isolation.md) | AFK | ready | 02 |
| 04 | [Four-copy repository create](./04-four-copy-repository-create.md) | AFK | ready | 01, 03 |
| 05 | [Encrypted quorum push](./05-encrypted-quorum-push.md) | AFK | ready | 04 |
| 06 | [Read/write routing split](./06-read-write-routing-split.md) | AFK | ready | 04 |
| 07 | [Hot promotion and cold recovery](./07-hot-promotion-and-cold-recovery.md) | AFK | ready | 05, 06 |
| 08 | [RF=3 to RF=4 background backfill](./08-rf3-to-rf4-background-backfill.md) | AFK | ready | 05 |
| 09 | [Delete, rebalance, and anti-entropy extensions](./09-delete-rebalance-and-anti-entropy-extensions.md) | AFK | ready | 05, 07 |
| 10 | [Admin UI four-copy replication status](./10-admin-ui-four-copy-replication-status.md) | AFK | ready | 07 |
| 11 | [Phase 1 E2E and integration tests](./11-phase-1-e2e-and-integration-tests.md) | AFK | ready | 09, 10 |
| 12 | [Org storage node registration and capacity](./12-org-storage-node-registration-and-capacity.md) | AFK | ready | 11 |
| 13 | [Self-host tier placement](./13-self-host-tier-placement.md) | AFK | ready | 12 |
| 14 | [Org quota credits and placement settings UI](./14-org-quota-credits-and-placement-settings-ui.md) | AFK | ready | 12 |
| 15 | [Cross-org encrypted placement algorithm](./15-cross-org-encrypted-placement-algorithm.md) | AFK | ready | 13, 14 |
| 16 | [Per-repo byte overrides and capacity engine](./16-per-repo-byte-overrides-and-capacity-engine.md) | AFK | ready | 15 |
| 17 | [Org node capacity shrink via platform rebalance](./17-org-node-capacity-shrink-via-rebalance.md) | discussion | backlog | 14, 09 |

## Dependency graph

```
01 ─┐
02 → 03 → 04 → 05 → 07 → 09 → 11
         ↘ 06 ↗    ↘ 08
              07 → 10
11 → 12 → 13 → 15 → 16
      ↘ 14 ↗
```

## Phases

| Phase | Issues | Scope |
|-------|--------|-------|
| 1 | 01–11 | Four-copy encrypted replication on platform fleet; RF=3 migration |
| 2 | 12–14 | Org-contributed storage nodes, self-host tiers, quota credits |
| 3 | 15–16 | Cross-org community hosting, capacity-aware placement, per-repo byte overrides |

## Source

[docs/prd/encrypted-replica-storage.md](../../prd/encrypted-replica-storage.md)
