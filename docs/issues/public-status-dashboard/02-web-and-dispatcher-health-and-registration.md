# Web and git dispatcher health and registration

## Metadata

- ID: status-02
- Type: AFK
- Status: ready
- Source: docs/prd/public-status-dashboard.md

## Parent

[PRD: Public Status Dashboard](../../prd/public-status-dashboard.md)

## What to build

Extend fleet auto-discovery to web and git dispatcher instances end-to-end:

1. **Dispatcher health endpoint** — lightweight anonymous `GET /health` returning 200 when the dispatcher process is ready.
2. **Web health endpoint** — lightweight anonymous health path on the Nuxt server (dedicated `/health` route or equivalent) returning 200 when the web app is serving.
3. **Dispatcher self-registration** — on startup, register with fleet registry (component type Git, instance id from `DispatcherId` config, probe URL to local health endpoint) and run periodic heartbeats.
4. **Web self-registration** — on Nitro/server startup, register (component type Website, instance id from env/hostname, probe URL to local health endpoint) and run periodic heartbeats.
5. **Tests** — health endpoints return 200; after startup both component types appear in fleet registry with correct ids.

Verifiable in compose or integration tests: registry lists `web-1`, `web-2`, `dispatcher-1`, `dispatcher-2` alongside API instances from slice 01.

## Acceptance criteria

- [ ] Git dispatcher exposes anonymous `GET /health` with 200 when healthy
- [ ] Web app exposes anonymous health path with 200 when healthy
- [ ] Dispatcher registers and heartbeats using configured `DispatcherId`
- [ ] Each web replica registers and heartbeats with distinct instance id
- [ ] Registry queries show Website and Git instances after stack boot
- [ ] Tests cover health endpoints and registration presence

## Blocked by

- [01-fleet-component-registry-and-api-self-registration.md](./01-fleet-component-registry-and-api-self-registration.md) (status-01)

## User stories covered

- 22 — Web instances register and heartbeat on startup
- 23 — Git dispatcher instances register and heartbeat on startup
- 44 — Dispatcher exposes health HTTP endpoint for probes
- 45 — Web exposes health path distinguishable from generic TCP reachability

## Notes

- Web registration may require a Nitro plugin or server middleware that calls the internal registration API on boot; use internal Docker network base URL for the control plane.
- Reuse the registration client pattern established in slice 01 rather than duplicating HTTP logic in each app.
- Storage nodes continue using the existing storage-node registry — do not register storage via fleet component registry.
