# sow-05 handoff — Retention prune and 90-day archive affordance

## What shipped

- `StatusAggregatorService.TryRunAggregationCycleAsync`: calls
  `StatusOutageWindowService.PruneOlderThanAsync(TimeSpan.FromDays(_options.HistoryRetentionDays))`
  right after `ApplySnapshotAsync` (and before `PersistSnapshotAsync`), reusing the same
  `StatusProbeOptions.HistoryRetentionDays` (default 90) already used by `StatusHistoryService` for
  hourly bucket retention, so window and chart retention stay in lockstep and configurable together.
- Fixed a real bug in `StatusOutageWindowService.PruneOlderThanAsync`: the `Where` clause mixed
  `||` with nullable `DateTimeOffset` comparisons, which SQLite's EF Core provider cannot translate
  (same class of issue hit in sow-02/sow-04). Now fetches candidates and filters in-memory,
  matching the pattern already used by `ListPublicWindowsAsync`/`ListAdminWindowsAsync`. This bug
  meant the prune call would have thrown at runtime the first time the aggregator tick ran it - the
  new prune-boundary tests caught it before it shipped.
- Web: `StatusOutageTimeline.vue` gained a 7d/30d/90d archive control (`update:days` emit) next to
  the UTC/local toggle, shown whenever a `days` prop is supplied. `status.vue` holds `windowsDays`
  as reactive state, refetches only the windows list (`refreshWindows()`) on archive selection
  without re-fetching the snapshot/history, and keeps polling with the user's selected lookback on
  the existing 30s timer. Default stays 7 days on load.
- i18n: `status.timeline.archiveDays` ("{days}d") label.
- Visual: gallery/timeline/status-page baselines re-generated for the new archive control.

## Tests run

- New: `StatusOutageWindowServiceTests` prune-boundary cases -
  `PruneOlderThanAsync_ClosedWindowPastRetention_IsRemoved`,
  `PruneOlderThanAsync_ClosedWindowWithinRetention_IsKept`,
  `PruneOlderThanAsync_OpenPublicWindow_IsNeverPrunedRegardlessOfAge` (open+public windows are
  never pruned regardless of age - only closed windows past retention, or stale never-public
  tracking records, are removed).
- `dotnet test tests/OpenGitBase.Features.Status.Tests` — 51 passed (48 existing + 3 new).
- `npx vue-tsc --noEmit`, `npx vitest run` (137 passed), `npx playwright test` (166 passed; the
  same 15 pre-existing font/anti-aliasing flaky tests remain, reproduced without any of this PRD's
  changes via `git stash` earlier in the sow-04 slice).

## PRD closeout

All five slices (sow-01 through sow-05) are complete on `main`. No hourly backfill was added (out
of scope, unchanged); public default remains 7 days; archive reaches up to 90 days via the same
`GET /public/status/windows?days=` endpoint from sow-02.
