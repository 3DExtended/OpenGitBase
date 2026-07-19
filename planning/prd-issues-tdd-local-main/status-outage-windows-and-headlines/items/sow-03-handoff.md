# sow-03 handoff — Public /status outage timeline UI

## What shipped

- `publicStatus.ts`: `PublicStatusOutageWindowDto` type; `PublicStatusSnapshot.openWindows` field
  (frontend type now matches the backend snapshot DTO from sow-01).
- `api.ts`: `status.getWindows(days)` client method against `GET /public/status/windows?days=`;
  `AdminStatusOutageWindowDto` now extends the public DTO with `suppressed` to avoid duplication.
- New `StatusOutageTimeline.vue`: primary outage list (open + closed, non-partial) ordered as
  returned by the API (open group -> closed group -> partial), secondary "Partial issues" section
  for instance-level windows, UTC/local timezone toggle (UTC default), duration as secondary
  metadata, annotation line when set, and an expand affordance for open group windows that shows
  currently-unhealthy instances from the live snapshot (redaction unchanged - reuses the same
  `StatusGroupSnapshot.instances` the group panel already renders). Empty state copy when no
  windows in the lookback window.
- `status.vue`: fetches `api.status.getWindows(7)` alongside the existing snapshot/history polls
  and renders `StatusOutageTimeline` between the live group panels and `StatusHistoryCharts`,
  matching the PRD stacking order (banner -> live -> timeline -> demoted % -> charts).
- MSW: `GET /api/public/status/windows` handler (open/closed-annotated/partial fixture) and
  `openWindows: []` added to the existing `/api/public/status` fixture for type parity.
- Visual: new `visual-status-outage-timeline` (populated) and `visual-status-outage-timeline-empty`
  gallery sections + `tests/visual/status-outage-timeline.spec.ts` baselines; `visual-gallery` and
  `status-page` baselines re-generated for the new page content.

## Notes on "expand" scope

Only open, group-scoped windows can expand to instance detail: the detector does not persist
per-instance windows while a group is already Unhealthy (see `OutageWindowDetector.ShouldTrack`),
so there is no stored instance history for a closed window to expand into. Expand therefore reads
live `snapshot.groups[].instances` for the matching group and only appears when there is currently
an Unhealthy instance to show - consistent with "live redacted labels" in the PRD (story 7).

## Tests run

- `npx vue-tsc --noEmit` — no new errors (three pre-existing errors in
  `useSidebarWorkspace.ts`, `__visual__/index.vue` storage-node fixtures, and `cli/auth.vue` were
  confirmed present before this slice via `git stash`).
- `npx vitest run` (opengitbase-web) — 137 passed, no regressions.
- `npx playwright test` (opengitbase-web) — new timeline visuals green; `visual-gallery` and
  `public status page` re-baselined for the new section (legitimate layout growth); remaining 15
  failures are the same pre-existing font/anti-aliasing flakiness from sow-04 (reproduced
  identically without this slice's changes), unrelated to sow-03.
- `dotnet test tests/OpenGitBase.Features.Status.Tests` — 48 passed (untouched by this
  frontend-only slice, re-run to confirm no drift).
