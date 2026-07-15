<!-- forge: #30 -->

# Storage page fleet replication card

## Metadata

- ID: admin-repl-02
- Type: AFK
- Status: ready
- Source: docs/prd/admin-replication-ui.md

## Parent

[PRD: Admin Replication UI](../../prd/admin-replication-ui.md)

## What to build

Extend the existing **Storage** admin page with a fleet replication card that consumes live APIs (no raw endpoint hints):

1. **Fleet gate banner** — show healthy node count vs RF=3 minimum (e.g. “3/3 nodes healthy — RF=3 eligible”) using registered storage node health.
2. **Enhanced node cards** — merge existing node list with `GET /admin/storage-nodes/replication-summary`: primary repository count, replica repository count, spare-capacity badge.
3. **Aggregate rollup** — counts per `ReplicationState` (e.g. “12 RF=3 healthy · 2 backfilling · 1 degraded”), derived client-side from the list API (acceptable v1 approach per PRD).
4. **Attention teaser** — up to five repositories needing attention, severity-sorted, loaded from list API with appropriate query params; each row links to `/admin/repositories/{id}`; “View all →” links to `/admin/repositories` with matching attention filter.
5. **Auto-refresh** — poll replication-summary + list teaser data every 30 seconds while the page is mounted; retain manual Refresh.
6. **Cleanup** — remove placeholder text that points operators at raw `GET /admin/...` paths.

Add API client methods and i18n strings needed for this page section.

## Acceptance criteria

- [ ] Storage page shows fleet gate, enhanced node cards, state rollup, and attention teaser
- [ ] Teaser uses server-side attention/severity semantics from slice 01 (not reimplemented in the browser)
- [ ] At most five repos in teaser; “View all →” navigates to repository index with sensible filter
- [ ] 30-second auto-refresh on fleet card data; interval cleared on unmount
- [ ] Placeholder API path hint text removed from storage page
- [ ] Page renders without error when no repos need attention or fewer than three nodes exist
- [ ] Existing storage sections (enrollments, fleet keys) unchanged in scope

## Blocked by

- [01-repository-replication-list-api.md](./01-repository-replication-list-api.md)

## User stories covered

- 1 — Fleet RF=3 eligibility gate
- 2 — Per-node primary/replica counts and spare indicator
- 3 — Fleet-wide replication state rollup
- 4 — Short attention list on storage page
- 5 — Severity-sorted teaser
- 6 — “View all repositories” link
- 7 — 30-second auto-refresh on storage fleet card
- 26 — Storage tile remains focused on provisioning (replication is a card, not a full repo table)

## Notes

- `GET /admin/storage-nodes/replication-summary` already exists; wire it into node cards.
- Rollup may call list API with broad params and aggregate `replicationState` counts client-side, or issue a minimal second request — avoid N+1 per repository.
- Teaser request: e.g. first page sorted by severity with page size 5, or dedicated filter if list API supports a combined “needs attention” view.
