# E2E smoke and cross-surface polish

## Metadata

- ID: status-09
- Type: AFK
- Status: ready
- Source: docs/prd/public-status-dashboard.md

## Parent

[PRD: Public Status Dashboard](../../prd/public-status-dashboard.md)

## What to build

Close the feature with regression protection and cross-surface consistency end-to-end:

1. **Tier0 compose smoke** — extend infrastructure smoke tests: public status returns 200 with expected top-level shape; public history returns 200; registered components appear after stack boot.
2. **OpenAPI / web client** — ensure swagger and generated or hand-written web client types include public status, history, and admin incident endpoints.
3. **Playwright smoke (optional but recommended)** — `/status` renders overall badge and groups; footer link navigates to `/status`; incident banner visible when seeded via admin API.
4. **Admin navigation** — add `/admin/status` entry point from admin home or admin index if not already linked in slice 08.
5. **Documentation touch** — brief mention in project README or web README of `/status` page (one paragraph, link to PRD).

Verify full happy path in compose: stack up → public status shows five groups → admin sets banner → banner appears on `/status` → charts render with history data.

## Acceptance criteria

- [ ] Tier0 (or equivalent) E2E test hits public status endpoint and asserts 200 + minimal shape
- [ ] E2E test hits public history endpoint and asserts 200
- [ ] OpenAPI documents new public and admin incident endpoints
- [ ] Web API client exposes public status and history methods used by status page
- [ ] Playwright or documented manual smoke checklist covers footer link and status page render
- [ ] Admin can reach `/admin/status` from admin navigation
- [ ] No regression to existing `GET /health` load-balancer behavior

## Blocked by

- [06-public-status-page-live-tree.md](./06-public-status-page-live-tree.md) (status-06)
- [07-history-charts-on-status-page.md](./07-history-charts-on-status-page.md) (status-07)
- [08-admin-incident-banner.md](./08-admin-incident-banner.md) (status-08)

## User stories covered

- Cross-cutting regression coverage for stories 1–43 (integration verification)
- 49 — `/admin/storage` remains deep drill-down (regression: no removal or breakage)

## Notes

- Prior art: `InfrastructureSmokeTests.ApiHealthReturnsHealthy`, `InternalNetworkMiddlewareIntegrationTests`.
- This slice is intentionally last — do not block earlier slices on E2E infrastructure.
- Visual snapshot for status page optional; Tier0 API smoke is minimum bar.
