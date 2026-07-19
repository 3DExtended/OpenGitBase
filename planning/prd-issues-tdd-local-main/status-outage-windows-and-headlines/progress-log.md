# Progress log: Status outage windows and headlines

## 2026-07-19

- Execution plan written; branch `main`.
- **sow-01** (#221) completed — commit `0eefe78`. Detector + store + openWindows. Compose unavailable (Docker daemon down); unit/integration tests green (10).
- **sow-02** (#222) completed. `GetPublicStatusWindowsQuery`/handler,
  `StatusOutageWindowService.ListPublicWindowsAsync`, `GET /public/status/windows?days=` on
  `PublicStatusController`. SQLite handler tests for suppress-omit, days boundary/clamp, and
  open/closed/partial ordering (48 status feature tests green). See `items/sow-02-handoff.md`.
- **sow-04** (#223) completed. Admin list/suppress/unsuppress/annotation queries + handlers,
  `AdminStatusController` routes under `admin/status/windows` (admin-role gated), admin
  `/admin/status` window list UI with suppress toggle and annotation input, MSW handlers, and a
  new `visual-admin-outage-windows` gallery snapshot. Backend tests 48 (status) + 10 (API)
  green; frontend unit tests 137 green; Playwright visuals green apart from pre-existing
  font-rendering flakiness unrelated to this slice. See `items/sow-04-handoff.md`.
- Remaining sow-03 → sow-05 in progress on main.
