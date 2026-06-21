# Admin navigation and repository index

## Metadata

- ID: admin-repl-03
- Type: AFK
- Status: ready
- Source: docs/prd/admin-replication-ui.md

## Parent

[PRD: Admin Replication UI](../../prd/admin-replication-ui.md)

## What to build

Add operator entry point and the full **repository replication index**:

1. **Admin home tile** — second tile “Replication” linking to `/admin/repositories`; Storage tile unchanged.
2. **Repository index page** — admin-only route listing all repositories with replication summary columns: name, owner, state badge, replica count (e.g. 2/3), primary node, max lag, write quorum, oldest last synced, epoch.
3. **Derived progress bars** — provisioning bar (`replicaCount / 3`) and sync bar from watermark lag (monotonic as lag decreases; handle zero-watermark backfill gracefully).
4. **Server-driven controls** — filter chips (All, Backfilling, Degraded, Lagging, No quorum) map to list API `attention` param; search box maps to `search`; sort control maps to `sort`; pagination controls map to `page` / `pageSize` with total count display.
5. **Auto-refresh** — 30-second poll while index is mounted; manual refresh retained.
6. **Row navigation** — click row → `/admin/repositories/{id}`.
7. **API client + i18n** — typed list method, replication state labels, column headers, empty states, filter labels.

## Acceptance criteria

- [ ] Admin home shows Replication tile → `/admin/repositories`
- [ ] Index displays all PRD columns and derived progress bars
- [ ] Filter chips, search, sort, and pagination drive server query params (not client-only filtering)
- [ ] Default sort is severity; total count visible in pager
- [ ] 30-second auto-refresh; cleared on unmount
- [ ] Rows link to repository detail route
- [ ] Empty and loading states render without error
- [ ] i18n keys added for replication UI strings introduced in this slice

## Blocked by

- [01-repository-replication-list-api.md](./01-repository-replication-list-api.md)

## User stories covered

- 8 — Dedicated repository index without knowing GUIDs
- 14 — Summary columns for triage
- 15 — Provisioning progress bar
- 16 — Sync progress bar
- 17 — 30-second index auto-refresh
- 18 — Pagination with total count
- 25 — Separate Replication admin tile
- 31 — Backfill progress from existing fields
- 32 — Distinct provisioning vs sync progress

## Notes

- Reuses list API from slice 01 exclusively for table data — no N+1 detail calls.
- URL query params should reflect filters/search/page so teaser “View all” links and browser back behave predictably.
- Sync bar formula when watermarks are zero: show 0% or indeterminate until first watermark (pick one; document in PR if needed).
