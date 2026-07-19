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
- **sow-03** (#224) completed. `PublicStatusOutageWindowDto` type + `status.getWindows(days)`
  client, new `StatusOutageTimeline.vue` (primary/partial split, UTC/local toggle, duration and
  annotation metadata, live-instance expand for open windows, empty state), wired into
  `status.vue` between live groups and charts. MSW handler + gallery fixtures + Playwright
  visuals (new timeline specs green; `visual-gallery`/`status-page` re-baselined for the new
  section). Frontend unit tests 137 green; backend 48 status tests unaffected. See
  `items/sow-03-handoff.md`.
- **sow-05** (#225) completed. Aggregator tick now calls
  `StatusOutageWindowService.PruneOlderThanAsync(HistoryRetentionDays)` after `ApplySnapshotAsync`;
  fixed a real SQLite LINQ-translation bug in `PruneOlderThanAsync` found by new prune-boundary
  tests. Public timeline gained a 7d/30d/90d archive control that refetches only the windows list.
  51 status feature tests green (48 + 3 new); frontend unit 137 green; Playwright visuals green
  apart from the same pre-existing flakiness. See `items/sow-05-handoff.md`.
- All slices (sow-01..sow-05) complete on main.
