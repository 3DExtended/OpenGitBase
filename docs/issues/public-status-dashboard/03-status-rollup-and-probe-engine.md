# Status rollup and probe engine

## Metadata

- ID: status-03
- Type: AFK
- Status: ready
- Source: docs/prd/public-status-dashboard.md

## Parent

[PRD: Public Status Dashboard](../../prd/public-status-dashboard.md)

## What to build

Deliver testable deep modules for probing and status rollup without yet wiring the background aggregator or public API:

1. **Status probe engine** — given probe targets (HTTP URL or TCP host/port), execute checks with configurable timeout (default 5s); return per-instance duration, success/failure, and message. Classify HTTP results as Healthy (<2s), Degraded (2–5s), or Unhealthy (fail/timeout).
2. **Storage group adapter** — load storage nodes from existing storage-node registry; map to instance rows with `lastSeenAt` from `LastHeartbeatAt`, individual healthy/degraded/unhealthy per PRD heartbeat rules; compute fleet group status from healthy count (3/3 Healthy, 2/3 Degraded, 0–1/3 Unhealthy).
3. **Data store probe targets** — parse Postgres and Redis connection settings from existing configuration; probe connectivity with same Healthy/Degraded/Unhealthy slow thresholds.
4. **Status rollup engine** — combine instance results into five fixed groups (Website, Api, Git, Storage, Data stores); apply worst-child-wins per group and overall.
5. **Tests** — exhaustive unit tests for rollup semantics (storage 3/2/1 thresholds, slow probes, missing heartbeats, worst-child group and overall rules); probe engine tests with mocked HTTP/TCP.

No background service, public endpoint, or UI in this slice — modules consumed by slice 04.

## Acceptance criteria

- [ ] Probe engine returns duration and status for HTTP and TCP targets with timeout handling
- [ ] Slow HTTP probes (>2s, ≤5s) classify as Degraded; failures/timeouts as Unhealthy
- [ ] Storage adapter reads existing storage-node registry without duplicating registration
- [ ] Storage fleet group status follows 3/2/1 healthy-node rules
- [ ] Individual storage rows reflect stale heartbeat (>90s) as Degraded and missing/failed as Unhealthy
- [ ] Postgres and Redis targets derived from connection configuration without manual probe lists
- [ ] Rollup engine produces five groups plus overall status using worst-child semantics
- [ ] Unit tests cover all normative semantics rows from the PRD implementation decisions table

## Blocked by

- [01-fleet-component-registry-and-api-self-registration.md](./01-fleet-component-registry-and-api-self-registration.md) (status-01)

## User stories covered

- 15 — Storage group 3/2/1 healthy thresholds
- 16 — Individual storage node stale/missing heartbeat rules
- 17 — API/web/git slow probe Degraded classification
- 18 — Postgres/Redis connectivity Degraded vs Unhealthy
- 19 — Group worst-child rollup
- 20 — Overall worst-group rollup
- 24 — Storage uses existing storage-node registry
- 25 — Postgres/Redis parsed from connection config
- 51 — Public storage health aligns with existing control-plane heartbeats

## Notes

- Keep rollup and probe engines free of EF and HTTP host dependencies where possible for fast unit tests.
- Fleet component registry (slice 01) feeds Website, Api, and Git instance lists; storage adapter is separate.
- Public DTO redaction happens in slice 04, not here.
