# Public status dashboard — implementation issues

Vertical slices for [PRD: Public Status Dashboard](../../prd/public-status-dashboard.md).

Implement in dependency order; each issue lists explicit blockers.

| ID | Issue | Type | Status | Blocked by |
|----|-------|------|--------|------------|
| 01 | [Fleet component registry and API self-registration](./01-fleet-component-registry-and-api-self-registration.md) | AFK | ready | — |
| 02 | [Web and git dispatcher health and registration](./02-web-and-dispatcher-health-and-registration.md) | AFK | ready | 01 |
| 03 | [Status rollup and probe engine](./03-status-rollup-and-probe-engine.md) | AFK | ready | 01 |
| 04 | [Background aggregator and public status API](./04-background-aggregator-and-public-status-api.md) | AFK | ready | 02, 03 |
| 05 | [Hourly history aggregation and public history API](./05-hourly-history-and-public-history-api.md) | AFK | ready | 04 |
| 06 | [Public status page with live component tree](./06-public-status-page-live-tree.md) | AFK | ready | 04 |
| 07 | [90-day history charts on status page](./07-history-charts-on-status-page.md) | AFK | ready | 05, 06 |
| 08 | [Admin incident banner](./08-admin-incident-banner.md) | AFK | ready | 04 |
| 09 | [E2E smoke and cross-surface polish](./09-e2e-smoke-and-polish.md) | AFK | ready | 06, 07, 08 |

## Dependency graph

```
01 → 02 ──┐
01 → 03 ──┼→ 04 → 05 → 07
          │     ↓
          │     ├→ 06 ──┐
          │     └→ 08 ──┼→ 09
          │             │
          └─────────────┘
```

Slices 02 and 03 may proceed in parallel after 01 completes. Slices 06 and 08 may proceed in parallel after 04 completes.

## Source

[docs/prd/public-status-dashboard.md](../../prd/public-status-dashboard.md)
