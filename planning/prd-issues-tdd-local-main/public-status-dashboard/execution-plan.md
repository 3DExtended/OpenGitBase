# Execution plan: Public Status Dashboard

**Source PRD:** [docs/prd/public-status-dashboard.md](../../docs/prd/public-status-dashboard.md)  
**Work items:** [docs/issues/public-status-dashboard/README.md](../../docs/issues/public-status-dashboard/README.md)

Branch strategy: **main** (all work items committed sequentially on default branch).

## Dependency order

| Order | ID | Title | Type | Status | Blocked by |
|-------|-----|-------|------|--------|------------|
| 1 | status-01 | Fleet component registry and API self-registration | AFK | completed | — |
| 2 | status-02 | Web and git dispatcher health and registration | AFK | ready | 01 |
| 3 | status-03 | Status rollup and probe engine | AFK | ready | 01 |
| 4 | status-04 | Background aggregator and public status API | AFK | ready | 02, 03 |
| 5 | status-05 | Hourly history aggregation and public history API | AFK | ready | 04 |
| 6 | status-06 | Public status page with live component tree | AFK | ready | 04 |
| 7 | status-07 | 90-day history charts on status page | AFK | ready | 05, 06 |
| 8 | status-08 | Admin incident banner | AFK | ready | 04 |
| 9 | status-09 | E2E smoke and cross-surface polish | AFK | ready | 06, 07, 08 |

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
