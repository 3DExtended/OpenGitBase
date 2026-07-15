<!-- forge: #184 -->

# Admin incident banner

## Metadata

- ID: status-08
- Type: AFK
- Status: ready
- Source: docs/prd/public-status-dashboard.md

## Parent

[PRD: Public Status Dashboard](../../prd/public-status-dashboard.md)

## What to build

Deliver operator incident messaging end-to-end:

1. **Incident banner service** — at most one active banner; fields: message (plain text, max ~500 chars), severity (`info`, `warning`, `outage`), timestamps; set replaces previous active banner; resolve deactivates/clears.
2. **Admin API** — admin-only endpoints to get active banner, set banner, and resolve banner.
3. **Public snapshot integration** — include active incident in public status snapshot (`incident` object or `null`).
4. **Admin page `/admin/status`** — form to compose message and severity, submit, resolve; link to public `/status` preview; link to `/admin/storage` for fleet drill-down.
5. **Public status page** — render incident banner at top with severity-driven styling when present.
6. **Tests** — admin auth required; set/replace/resolve behavior; public snapshot includes incident; non-admin cannot mutate.

No incident history log in this slice — resolve clears the banner without archive.

## Acceptance criteria

- [ ] Admin can set an active incident banner with message and severity
- [ ] Setting a new banner replaces any existing active banner
- [ ] Admin can resolve (clear) the active banner in one action
- [ ] Incident endpoints require admin role
- [ ] Public status snapshot includes `incident` when active, `null` when none
- [ ] `/status` page displays incident banner with severity styling when active
- [ ] `/admin/status` page provides compose, resolve, and link to public preview
- [ ] API tests cover set, replace, resolve, and auth

## Blocked by

- [04-background-aggregator-and-public-status-api.md](./04-background-aggregator-and-public-status-api.md) (status-04)

## User stories covered

- 13 — Operator incident banner visible on public page
- 38 — Admin `/admin/status` compose form
- 39 — Resolve action clears banner
- 40 — At most one active banner
- 41 — Preview/link to public `/status`
- 43 — Admin-only incident management

## Notes

- Incident banner table created in slice 01 migration; this slice wires CRUD and UI.
- Can proceed in parallel with slice 06 after slice 04 lands.
- Admin home tile for `/admin/status` optional — link from admin index or storage page acceptable for v1.
