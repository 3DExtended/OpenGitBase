# Handoff: status-01

## PRD

- [docs/prd/public-status-dashboard.md](../../../../docs/prd/public-status-dashboard.md)

## Work item

- [docs/issues/public-status-dashboard/01-fleet-component-registry-and-api-self-registration.md](../../../../docs/issues/public-status-dashboard/01-fleet-component-registry-and-api-self-registration.md)

## Acceptance criteria

- Migration: fleet component, status history hourly bucket, incident banner tables
- Register upserts by (componentType, instanceId)
- Heartbeat updates lastHeartbeatAt; TTL stale handling
- Register/heartbeat 403 from external clients
- API self-registration on startup with periodic heartbeats
- Tests for registry, TTL, internal-network restriction

## Dependencies

- None

## Branch

`main`
