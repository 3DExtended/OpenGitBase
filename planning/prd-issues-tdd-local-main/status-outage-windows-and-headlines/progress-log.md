# Progress log: Status outage windows and headlines

## 2026-07-19

- Execution plan written; branch `main`.
- **sow-01** (#221) completed — commit `0eefe78`. Detector + store + openWindows. Compose unavailable (Docker daemon down); unit/integration tests green (10).
- **sow-02** (#222) completed. `GetPublicStatusWindowsQuery`/handler,
  `StatusOutageWindowService.ListPublicWindowsAsync`, `GET /public/status/windows?days=` on
  `PublicStatusController`. SQLite handler tests for suppress-omit, days boundary/clamp, and
  open/closed/partial ordering (48 status feature tests green). See `items/sow-02-handoff.md`.
- Remaining sow-04 → sow-03 → sow-05 in progress on main.
