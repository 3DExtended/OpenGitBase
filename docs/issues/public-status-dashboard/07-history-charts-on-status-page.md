# 90-day history charts on status page

## Metadata

- ID: status-07
- Type: AFK
- Status: ready
- Source: docs/prd/public-status-dashboard.md

## Parent

[PRD: Public Status Dashboard](../../prd/public-status-dashboard.md)

## What to build

Add 90-day history visualization to the public status page end-to-end:

1. **Chart library** — introduce a lightweight chart dependency suitable for Nuxt (responsive, accessible colors, client-side render acceptable).
2. **Uptime line chart** — overall uptime % line by default; toggles to overlay each component group line; 90-day daily x-axis.
3. **Stacked state chart** — daily mix of Healthy / Degraded / Unhealthy for overall platform (stacked area or bar).
4. **History polling** — fetch public history API on mount (30s refresh optional or on page load only — prefer load + manual refresh or same 30s poll as status).
5. **Partial data** — render gracefully when fewer than 90 days of buckets exist (short line, no broken axes).
6. **i18n** — chart titles, legend labels, toggle copy.

Verifiable in browser against compose with seeded history or after aggregator has run.

## Acceptance criteria

- [ ] Status page displays uptime line chart with overall series visible by default
- [ ] User can toggle individual group lines (Website, Api, Git, Storage, Data stores)
- [ ] Stacked overall Healthy/Degraded/Unhealthy chart renders below or alongside line chart
- [ ] Charts consume public history API data (not client-computed from live snapshot)
- [ ] Partial history (< 90 days) renders without errors
- [ ] Chart colors work in light and dark theme where applicable
- [ ] English i18n for chart labels

## Blocked by

- [05-hourly-history-and-public-history-api.md](./05-hourly-history-and-public-history-api.md) (status-05)
- [06-public-status-page-live-tree.md](./06-public-status-page-live-tree.md) (status-06)

## User stories covered

- 10 — 90-day uptime chart with overall line and group toggles
- 11 — Stacked daily Healthy/Degraded/Unhealthy mix chart
- 12 — Graceful partial data when system younger than 90 days

## Notes

- Chart library choice is an implementation decision (Chart.js, Unovis, ECharts, etc.) — pick one dependency, avoid bundling multiple.
- If history API returns empty series, show empty state copy rather than blank broken chart.
