# Background aggregator and public status API

## Metadata

- ID: status-04
- Type: AFK
- Status: ready
- Source: docs/prd/public-status-dashboard.md

## Parent

[PRD: Public Status Dashboard](../../prd/public-status-dashboard.md)

## What to build

Wire probing, rollup, and public read access into a running system end-to-end:

1. **Advisory lock leader election** — Postgres advisory lock so exactly one API instance runs the aggregator; automatic failover when lock holder stops.
2. **Status aggregator background service** — every ~30s (lock holder only): load fleet components and storage nodes, run probe engine, rollup, write latest snapshot (persisted row or dedicated snapshot store).
3. **Public status projection** — map internal probe/registry data to redacted public DTO: instance id, group, status, last checked, response time, last seen (storage), message; exclude internal hosts, ports, certs, disk bytes.
4. **Public API** — anonymous `GET /api/v1/public/status` returning overall status, timestamp, five groups with instances, and `incident: null` placeholder until slice 08.
5. **Rate limiting** — apply appropriate rate limiting on public status endpoint consistent with other anonymous surfaces.
6. **Tests** — advisory lock exclusivity; aggregator tick produces expected snapshot; public endpoint returns redacted shape; external registration still 403.

Demo: with compose stack running, `curl` public status shows all five groups with named instances and correct rollup.

## Acceptance criteria

- [ ] Only one API instance holds aggregator lock and executes probes at a time
- [ ] Lock releases on shutdown; another API instance acquires lock within reasonable interval
- [ ] Aggregator runs on ~30s interval and writes latest snapshot
- [ ] Snapshot includes Website, Api, Git (from fleet registry), Storage (from storage-node registry), and Data stores (postgres, redis)
- [ ] `GET /api/v1/public/status` is anonymous and returns redacted public DTO
- [ ] Public response omits internal host, ports, certificate thumbprint, enrollment tokens, and disk metrics
- [ ] Public endpoint is rate-limited
- [ ] Integration or unit tests cover lock behavior, snapshot write, and redaction

## Blocked by

- [02-web-and-dispatcher-health-and-registration.md](./02-web-and-dispatcher-health-and-registration.md) (status-02)
- [03-status-rollup-and-probe-engine.md](./03-status-rollup-and-probe-engine.md) (status-03)

## User stories covered

- 27 — Optional config override (defer unless trivial; not required for acceptance)
- 28 — Postgres advisory lock for single aggregator
- 29 — Automatic failover to another API instance
- 30 — 30-second probe interval
- 31 — Fast public reads from stored snapshot
- 34 — Anonymous public status endpoint with groups and instances
- 37 — Public endpoints rate-limited
- 50 — Aggregator logs probe failures and lock changes

## Notes

- Primary integration slice — consumes modules from slice 03 and fleet data from slices 01–02.
- Incident banner field in snapshot may be `null` until slice 08; include the shape now for stable API contract.
- Hourly history writes deferred to slice 05.
