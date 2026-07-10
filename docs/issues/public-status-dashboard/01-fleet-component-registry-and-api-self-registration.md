# Fleet component registry and API self-registration

## Metadata

- ID: status-01
- Type: AFK
- Status: ready
- Source: docs/prd/public-status-dashboard.md

## Parent

[PRD: Public Status Dashboard](../../prd/public-status-dashboard.md)

## What to build

Deliver the fleet component registry foundation and API self-registration end-to-end:

1. **Schema migration** — introduce status-related entities in one migration: fleet component registry, hourly history buckets, and incident banner (tables may remain unused until later slices but avoid follow-up migrations for empty features).
2. **Fleet component registry** — persist registered instances by component type (Website, Api, Git); upsert on register; update `lastHeartbeatAt` on heartbeat; mark instances Unhealthy or stale when heartbeat TTL (default 90s) expires.
3. **Internal registration API** — register and heartbeat endpoints under an internal prefix, restricted by existing internal-network middleware (same posture as storage-node registration).
4. **API self-registration** — on API startup, register this instance (instance id from configuration or hostname, probe URL pointing at local `/health`) and run a periodic heartbeat loop.
5. **Tests** — registry upsert and TTL behavior; external client receives 403 on register/heartbeat; API instance appears in registry after startup in integration tests.

No public status page or aggregator in this slice — verifiable via internal API tests and registry queries.

## Acceptance criteria

- [ ] Migration creates fleet component, status history hourly bucket, and incident banner tables
- [ ] Register upserts by `(componentType, instanceId)` with probe URL and timestamps
- [ ] Heartbeat updates `lastHeartbeatAt`; instances beyond TTL are marked absent or Unhealthy per PRD semantics
- [ ] Register and heartbeat endpoints return 403 from non-internal clients
- [ ] Each API instance registers itself on startup and sends periodic heartbeats
- [ ] Unit or integration tests cover registry CRUD, TTL, and internal-network restriction

## Blocked by

- None — can start immediately

## User stories covered

- 21 — API instance self-registers with instance id and probe URL
- 26 — Stale registrations handled via heartbeat TTL
- 36 — Registration endpoints internal-network restricted
- 46 — API probed via existing `/health` endpoint (probe URL registered accordingly)

## Notes

- Primary deep module: fleet component registry with a small register/heartbeat command or query handler surface.
- Instance id should align with compose service names where possible (`api-1`, `api-2`).
- Optional config override for manual probe targets is out of scope for this slice (PRD escape hatch for later if needed).
