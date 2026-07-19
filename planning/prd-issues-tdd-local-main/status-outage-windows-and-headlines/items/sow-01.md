# sow-01 implementation record

## Summary

Implemented outage window detector (5m Unhealthy / 2m Healthy merge), `StatusOutageWindow` persistence, aggregator wiring under advisory lock, and `openWindows` on the live public snapshot DTO. Unit tests cover detector rules; SQLite integration test covers open window projection after 5 minutes with mocked clock.

## Linked Context

- PRD: `docs/prd/status-outage-windows-and-headlines.md`
- Work item: sow-01 (#221)

## Dependency Graph

### Direct dependencies (blocked by)

- None

### Full chain

`sow-01`

## Status

- Branch: `main`
- Tests: `dotnet test tests/OpenGitBase.Features.Status.Tests --filter OutageWindow|StatusOutageWindow` — 10 passed
- Visual snapshots: none
- Compose: API not running at commit time; migration `AddStatusOutageWindows` added for later apply
- Commit(s): (pending)
