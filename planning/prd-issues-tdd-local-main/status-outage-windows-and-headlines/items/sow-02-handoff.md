# Handoff: sow-02

- PRD: `docs/prd/status-outage-windows-and-headlines.md`
- Work item: sow-02 — Public windows history API
- Forge: #222
- Acceptance: see `docs/issues/sow-02.md`
- Blocked by: sow-01 (done)
- Branch: `main`

## What shipped

- `GetPublicStatusWindowsQuery` (Days 1..90, default 7) + `GetPublicStatusWindowsQueryHandler`
- `StatusOutageWindowService.ListPublicWindowsAsync(days, ct)`: clamps days to [1,90]; omits
  suppressed; includes open windows unconditionally and closed windows ended within the lookback
  cutoff; orders open-group -> closed-group -> partial (each descending by `UnhealthySince`)
- `GET /public/status/windows?days=` on `PublicStatusController` (anonymous, same rate limit as
  existing public status routes since it inherits the controller-level
  `[EnableRateLimiting("content-browse-anonymous")]`)
- Reused `PublicStatusOutageWindowDto`/`DisplayName` redaction path already established in sow-01
  (headline names resolved once at window-open time from the live public snapshot projection, so
  no raw hosts/ports ever land in `InstanceId`/`DisplayName`)

## Tests

- `GetPublicStatusWindowsQueryHandlerTests` (SQLite in-memory): suppressed omitted, default-days
  boundary excludes windows closed >7d ago, days clamped at 90, and ordering
  open-group/closed-group/partial verified end to end through the handler.

## Notes for later slices

- sow-04 (admin suppress/annotate) and sow-05 (prune) should keep extending
  `StatusOutageWindowService` rather than adding new EF queries elsewhere.
