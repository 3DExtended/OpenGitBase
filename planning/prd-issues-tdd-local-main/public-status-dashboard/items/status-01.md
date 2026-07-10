## Summary

Implemented fleet component registry foundation (status-01):

- New `OpenGitBase.Features.Status` feature slice with `FleetComponent`, `StatusIncidentBanner`, and `StatusHistoryHourlyBucket` entities and EF migration
- Internal `POST /api/v1/internal/fleet-components/register|heartbeat` and `GET` list endpoints (internal-network restricted)
- Register upsert by `(ComponentType, InstanceId)`, heartbeat updates, stale TTL marking (90s default)
- `ApiFleetComponentRegistrationService` self-registers API instances on startup with periodic heartbeats
- Compose env for `api-1` / `api-2` instance ids

## Linked Context

- PRD: `docs/prd/public-status-dashboard.md`
- Work item: `status-01`

## Dependency Graph

### Direct dependencies (blocked by)

- None

### Full chain

`status-01`

## Status

- Branch: `main`
- Tests passing:
  - `dotnet test tests/OpenGitBase.Features.Status.Tests` — 3 passed
  - `dotnet test tests/OpenGitBase.Api.Tests --filter FullyQualifiedName~FleetComponent|FullyQualifiedName~InternalNetworkMiddlewareIntegrationTests` — 8 passed
- Visual snapshots: none (backend-only slice)
- Compose: migration via `docker compose run --rm api-migrate` (see progress log)
