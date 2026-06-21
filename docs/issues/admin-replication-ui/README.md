# Admin replication UI — implementation issues

Vertical slices for [PRD: Admin Replication UI](../../prd/admin-replication-ui.md).

Implement in dependency order; each issue lists explicit blockers.

| ID | Issue | Type | Status | Blocked by |
|----|-------|------|--------|------------|
| 01 | [Repository replication list API](./01-repository-replication-list-api.md) | AFK | ready | — |
| 02 | [Storage page fleet replication card](./02-storage-page-fleet-replication-card.md) | AFK | ready | 01 |
| 03 | [Admin navigation and repository index](./03-admin-navigation-and-repository-index.md) | AFK | ready | 01 |
| 04 | [Repository replication detail page](./04-repository-replication-detail-page.md) | AFK | ready | 01 |
| 05 | [Cross-surface polish and regression smoke](./05-cross-surface-polish-and-regression-smoke.md) | AFK | ready | 02, 03, 04 |

## Dependency graph

```
01 → 02 ─┐
01 → 03 ─┼→ 05
01 → 04 ─┘
```

Slices 02, 03, and 04 may proceed in parallel after 01 completes.

## Source

[docs/prd/admin-replication-ui.md](../../prd/admin-replication-ui.md)

## Parent PRD relationship

Completes user stories **31** and **33** from [HA storage replication](../../prd/ha-storage-replication.md) and supersedes the partial delivery of [ha-storage-11](../ha-storage-replication/11-admin-ui-replication-status.md) (APIs shipped; UI did not).
