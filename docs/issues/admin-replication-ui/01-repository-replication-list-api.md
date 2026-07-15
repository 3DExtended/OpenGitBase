<!-- forge: #29 -->

# Repository replication list API

## Metadata

- ID: admin-repl-01
- Type: AFK
- Status: ready
- Source: docs/prd/admin-replication-ui.md

## Parent

[PRD: Admin Replication UI](../../prd/admin-replication-ui.md)

## What to build

Deliver the admin repository replication **list** surface end-to-end on the API:

1. **Attention matcher** — shared helper encoding “needs attention” predicate, attention preset filters (`all`, `backfilling`, `degraded`, `lagging`, `no-quorum`), and severity rank for default sort.
2. **List query handler (CQRS)** — projects paginated replication summaries by joining repositories, replica rows, and storage nodes; computes `maxWatermarkLag`, `oldestLastSyncedAt`, `writeQuorumAvailable` (consistent with existing routing/quorum logic), and human-readable `primaryNodeId`.
3. **Controller endpoint** — `GET /admin/repositories` (admin role required) accepting `page`, `pageSize`, `sort`, `search`, and `attention`; returns `{ items, totalCount, page, pageSize }`.
4. **Tests** — query handler and/or controller tests for pagination boundaries, each attention preset, search, alternate sorts, severity ordering, and summary field computation edge cases (healthy RF=3, backfilling with fewer than three replicas, healthy state with lagging replica, no write quorum).

No schema migrations. No web UI in this slice — verifiable via HTTP/admin tests.

## Acceptance criteria

- [ ] `GET /admin/repositories` returns paginated summary rows with all PRD fields (`repositoryId`, `name`, `ownerSlug`, `replicationState`, `replicaCount`, `primaryNodeId`, `primaryWatermark`, `maxWatermarkLag`, `writeQuorumAvailable`, `replicationEpoch`, `oldestLastSyncedAt`)
- [ ] Default sort is `severity` (no quorum → degraded/promoting → backfilling → lagging → healthy)
- [ ] `attention` presets filter server-side using the same rules documented in the PRD
- [ ] `search` matches repository name and owner slug case-insensitively
- [ ] `page` / `pageSize` paginate with stable `totalCount` (default page size 50, sensible max cap)
- [ ] Endpoint requires admin role, consistent with existing admin storage routes
- [ ] API tests cover pagination, filters, search, severity sort, and summary computation edge cases

## Blocked by

- None — can start immediately

## User stories covered

- 9 — Paginated server-side repository index data
- 10 — Default severity sort (problems first)
- 11 — Alternate sorts (name, lag, state)
- 12 — Server-driven filter chips (via `attention` param)
- 13 — Server-driven search by name / owner slug
- 27 — New admin list endpoint (no N+1 to detail)
- 28 — Attention presets aligned with teaser rules (server-defined)
- 29 — Admin-only access
- 30 — API tests for list behavior

## Notes

- Primary deep module: list query handler. Keep attention matcher as a small testable unit consumed by the handler.
- `writeQuorumAvailable` should reuse the same logic as `RepositoryReplicationRoutingQuery` where practical.
- Owner slug resolution uses existing repository ownership relations (user or organization).
- Fleet rollup counts for the storage page are **not** required in this slice; slice 02 may derive rollup client-side from list totals or a lightweight follow-up query.
