<!-- forge: #181 -->

# Hourly history aggregation and public history API

## Metadata

- ID: status-05
- Type: AFK
- Status: ready
- Source: docs/prd/public-status-dashboard.md

## Parent

[PRD: Public Status Dashboard](../../prd/public-status-dashboard.md)

## What to build

Persist status history and expose it for charts end-to-end:

1. **Hourly bucket upsert** — on each aggregator tick (slice 04), increment current hour bucket per component group with counts of Healthy, Degraded, and Unhealthy samples based on rolled-up group status; also increment overall bucket for stacked chart.
2. **Daily rollup query** — aggregate 24 hourly buckets into daily points for up to 90 days; compute uptime percentage per group for line chart (document and test chosen Degraded weight — default 0.5 in PRD open choices).
3. **Retention prune** — delete hourly buckets older than 90 days (inline or periodic).
4. **Public history API** — anonymous `GET /api/v1/public/status/history?days=90` returning daily series for overall and each group (uptime % plus Healthy/Degraded/Unhealthy ratios for stacked chart).
5. **Tests** — bucket increment/idempotency within same hour; daily rollup math; partial history when fewer than 90 days of data exist; prune removes old rows.

Verifiable via API tests and manual curl against a seeded or time-advanced test fixture.

## Acceptance criteria

- [ ] Aggregator tick upserts hourly buckets for all five groups plus overall
- [ ] Bucket schema stores healthy/degraded/unhealthy sample counts per period
- [ ] Daily rollup returns up to 90 days of data points per group
- [ ] Uptime percentage formula is documented in tests and consistent across endpoints
- [ ] History older than 90 days is pruned
- [ ] `GET /api/v1/public/status/history` is anonymous and rate-limited
- [ ] Response handles deployments younger than 90 days without error (partial series)
- [ ] Unit tests cover bucket math, daily aggregation, and retention

## Blocked by

- [04-background-aggregator-and-public-status-api.md](./04-background-aggregator-and-public-status-api.md) (status-04)

## User stories covered

- 32 — Hourly history buckets persisted per component group
- 33 — At least 90 days retention with automatic pruning
- 35 — Anonymous history endpoint for charts

## Notes

- Storage estimate ~11k rows — keep upserts efficient (single row per group per hour).
- Chart rendering deferred to slice 07; this slice is API + aggregator write path only.
