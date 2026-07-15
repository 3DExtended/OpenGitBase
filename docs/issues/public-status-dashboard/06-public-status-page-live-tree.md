<!-- forge: #182 -->

# Public status page with live component tree

## Metadata

- ID: status-06
- Type: AFK
- Status: ready
- Source: docs/prd/public-status-dashboard.md

## Parent

[PRD: Public Status Dashboard](../../prd/public-status-dashboard.md)

## What to build

Deliver the public `/status` page with live component health (no charts yet) end-to-end:

1. **Route and slug reservation** — add `/status` page; reserve `status` in slug validation alongside existing reserved routes.
2. **Status page UI** — overall status badge and last-updated timestamp; five collapsible component groups with named instance rows (status, last checked, response time, last seen, message).
3. **Auto-expand behavior** — Degraded and Unhealthy groups expanded on load; Healthy groups collapsed.
4. **Polling** — fetch public status API every 30 seconds while page is mounted.
5. **Global footer** — add site footer to default layout with “System status” link (i18n).
6. **Admin cross-link** — when authenticated admin, show link to `/admin/storage` (and later `/admin/status` from slice 08).
7. **Web client** — extend API client/types for public status endpoint.

Verifiable manually in browser against compose stack; optional lightweight Playwright smoke optional but not required for acceptance.

## Acceptance criteria

- [ ] `/status` loads without authentication and displays overall status
- [ ] Five groups render with collapsible sections and per-instance rows
- [ ] Degraded/Unhealthy groups auto-expand; Healthy groups collapsed on load
- [ ] Page polls public status API every 30 seconds
- [ ] Footer link to `/status` appears on default layout pages
- [ ] `status` slug reserved for org/repo creation
- [ ] Admin users see cross-link to admin fleet tools
- [ ] No internal hosts, ports, certs, or disk data rendered
- [ ] English i18n keys for status page and footer link

## Blocked by

- [04-background-aggregator-and-public-status-api.md](./04-background-aggregator-and-public-status-api.md) (status-04)

## User stories covered

- 1 — Anonymous `/status` page
- 2 — Overall status indicator
- 3 — Five component groups
- 4 — Collapsible named instances
- 5 — Auto-expand Degraded/Unhealthy groups
- 6 — Healthy groups collapsed by default
- 7 — Instance row fields (status, times, message)
- 8 — 30-second auto-refresh
- 9 — Footer link on all layouts
- 14 — No leaked internal topology on public page
- 42 — Admin cross-link when signed in as admin
- 47 — `/status` reserved top-level route
- 48 — Usable by guests with standard header

## Notes

- Charts deferred to slice 07; incident banner display deferred to slice 08 (page should tolerate `incident: null`).
- Match existing Nuxt UI patterns (explore page layout density, status badges from design system).
