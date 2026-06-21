# Repository replication detail page

## Metadata

- ID: admin-repl-04
- Type: AFK
- Status: ready
- Source: docs/prd/admin-replication-ui.md

## Parent

[PRD: Admin Replication UI](../../prd/admin-replication-ui.md)

## What to build

Add per-repository admin drill-down at **`/admin/repositories/{id}`**:

1. **Header** — repository name, owner slug, link to public repository page.
2. **Status block** — replication state badge, epoch, primary watermark, write-quorum flag (from existing `GET /admin/repositories/{id}/replication`).
3. **Visual replica layout** — primary node visually distinguished from secondary replicas (not a flat table only).
4. **Replica rows** — human-readable node id (`storage-1`), role, applied watermark, in-sync flag, last synced, lag delta (`primaryWatermark − appliedWatermark`).
5. **API enrichment (if needed)** — extend detail replica DTO with `nodeId` string resolved server-side; alternatively join client-side via cached storage node list — prefer server field if join is awkward in UI.
6. **Edge cases** — render cleanly for pre-backfill and mid-backfill repos (< 3 replicas, zero watermarks).
7. **Auto-refresh** — 30-second poll while detail page is mounted; manual refresh retained.
8. **API client + i18n** — detail fetch method and strings for this page.

## Acceptance criteria

- [ ] Detail route loads replication data for a valid repository id
- [ ] Header shows name, owner, and public repo link
- [ ] Status block shows state, epoch, primary watermark, write quorum
- [ ] Primary vs replica roles are visually obvious
- [ ] Each replica shows node id, watermarks, in-sync, last synced, lag delta
- [ ] Page handles fewer than three replicas and zero watermarks without error
- [ ] Unknown repository id shows appropriate not-found state
- [ ] 30-second auto-refresh; cleared on unmount
- [ ] API test added or updated if replica DTO gains `nodeId`

## Blocked by

- [01-repository-replication-list-api.md](./01-repository-replication-list-api.md)

## User stories covered

- 19 — Per-repo page with name, owner, public link
- 20 — State, epoch, watermark, write quorum
- 21 — Visual primary vs replica layout
- 22 — Replica rows with node id and lag delta
- 23 — 30-second detail auto-refresh
- 24 — Mid-backfill / legacy RF=1 states render safely

## Notes

- Existing detail endpoint covers most fields; this slice is primarily UI plus optional DTO enrichment.
- Index page (slice 03) should link here; no circular dependency — detail only needs list API for optional breadcrumb back link.
- Read-only observability — no promote, rebalance, or backfill trigger actions.
