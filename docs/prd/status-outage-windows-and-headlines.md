<!-- forge: #220 -->

# PRD: Status Outage Windows and Headlines

## Problem Statement

Public `/status` shows live health and 90-day uptime %, but % poorly answers what failed when. Visitors cannot scan grouped outage intervals (e.g. "Message bus down 12:00-17:00 UTC"). Hourly buckets/charts are not headlines. The manual incident banner is narrative only-not probe-derived.

## Solution

Add **auto-detected outage windows** as headlines, plus light operator controls:

1. **Timeline on `/status`** - primary history; demote %; keep charts. Group-named headlines; instances on expand. Open: "down since ..."; closed: UTC range; duration as side metadata.
2. **Online detection** in the status aggregator (advisory lock): group Unhealthy >=5 min opens; Healthy gaps <=2 min merge; >2 min Healthy closes. Persist windows; keep hourly buckets for charts.
3. **Partial issues** - instance Unhealthy >=5 min while group only Degraded → no group "down"; list under Partial issues.
4. **Data stores** - headline Postgres/Redis for single-store Unhealthy (not "Data stores").
5. **Banner** stays narrative on top; windows are factual below (no auto-banner).
6. **Admin suppress + annotate** on `/admin/status`; times immutable.
7. **APIs** - open windows on live snapshot; `GET /public/status/windows?days=` (page 7d; store 90d). Same redaction as live instances.
8. **Timezone** - UTC default + local toggle.

No hourly backfill. No Overall windows.

## User Stories

1. As a visitor, I want outage headlines on `/status`, so that I see what broke when without reading %.
2. As a visitor, I want open outages after >=5 min Unhealthy, so that ongoing incidents surface before recovery.
3. As a visitor, I want "{Group} down since {start} UTC" while open, so that I know it is active.
4. As a visitor, I want "{Group} down {start}-{end} UTC" when closed, so that I have a factual interval.
5. As a visitor, I want duration as secondary metadata, so that impact is clear without title clutter.
6. As a visitor, I want live group names in headlines (Website, API, Git, Storage, Message bus), so that vocabulary matches the panel.
7. As a visitor, I want expand to show Unhealthy instances with live redacted labels, so that drill-in stays safe.
8. As a visitor, I want Data stores headlines as Postgres/Redis when only one is Unhealthy, so that the page does not oversell.
9. As a visitor, I want Partial issues for instance Unhealthy under a Degraded group, so that quorum partial loss is visible without false "down".
10. As a visitor, I want ~7 days by default and up to 90 days archive, so that the page stays scannable yet reviewable.
11. As a visitor, I want order open group → closed group → partial, so that actionable facts lead.
12. As a visitor, I want the % callout demoted below the timeline, so that narrative history leads.
13. As a visitor, I want 90-day charts kept, so that trends remain.
14. As a visitor, I want the manual banner still on top when active, so that narrative is not replaced.
15. As a visitor, I want UTC by default and a local toggle, so that shared links agree and personal mapping works.
16. As a visitor, I want suppressed windows omitted and annotations shown when set, so that false positives and planned work are handled.
17. As a security-conscious operator, I want the same redaction as live instances, so that hosts/ports do not leak.
18. As the platform, I want Unhealthy-only windows (not Degraded), 5 min min open, <=2 min Healthy merge, >2 min Healthy close, so that detection matches locked thresholds.
19. As the platform, I want group windows from rolled-up Unhealthy for Website/API/Git/Storage/Message bus, so that headlines match rollup.
20. As the platform, I want instance windows for Partial issues and Data stores naming, and no Overall windows, so that edge cases stay honest without duplication.
21. As the platform, I want online detection under the aggregator lock with persisted window records (not raw 90d samples), so that one writer keeps precise open/closed state cheaply.
22. As the platform, I want hourly charts unchanged, 90d window prune, and no hourly backfill, so that charts and honesty are preserved.
23. As the web UI, I want open windows on the anonymous snapshot and `GET /public/status/windows?days=` (1-90) rate-limited, so that poll + archive are server-authoritative.
24. As the web UI, I want DTOs with id, scope, group, optional instance, start, optional end, open, duration, optional annotation, so that rendering needs no guesses.
25. As an admin, I want `/admin/status` window list with suppress, unsuppress, set/clear annotation, admin-visible suppressed rows, and immutable times, so that presentation can be fixed without rewriting clocks.
26. As a security reviewer, I want suppress/annotate admin-only, so that only operators alter public presentation.
27. As a visitor, I want a distinct timeline section and clear empty-state copy, so that facts vs probes vs trends are clear on day one.
28. As an admin, I want a link to public `/status`, so that I can verify changes.
29. As the web app, I want Playwright visuals for timeline and admin window controls, so that layout regressions are caught.
30. As an operator, I want open/close/merge logs and Message bus naming kept (no one-off Kafka rename), so that ops stay debuggable and consistent.

## Implementation Decisions

**Modules:** Outage window detector (pure; 5m/2m); window store (scope, group, instanceId?, startedAt, endedAt?, suppressed, annotation; prune >90d); aggregator wiring after locked probe/rollup; public windows query; admin suppress/annotate; public timeline UI + UTC/local + Partial issues; admin window table.

**Rules:** Group headlines from rolled-up Unhealthy; partial when instance Unhealthy and group not; Data stores single-store titles Postgres/Redis; no Overall; no backfill; Degraded-only never opens "down".

**API:** Snapshot `openWindows[]`; `GET /public/status/windows?days=`; admin under `admin/status/windows`. Redaction = live public projection. UI stack: banner → live → timeline → demoted % → charts.

## Testing Decisions

Prior art: `features/status`, API controller tests, web visuals. Prove detector thresholds/merge/close/no-Degraded/no-Overall/Data-stores/partial; aggregator tick → snapshot open windows; public days/suppress/redaction/rate-limit; admin auth + immutable times; visuals; empty timeline with intact charts. Public interfaces / single-tick + mocked clock-not private timers.

## Out of Scope

Auto-banner from windows; editing times/merge/split; Message bus→Kafka rename; hourly backfill; chart sub-hour zoom; raw 90d samples; Overall windows; maintenance-mode probe suppression; notifications/webhooks; multi-banner archive beyond windows.

## Further Notes

Incremental on Public Status Dashboard. Follow code's six groups (incl. Message bus). Locked: Unhealthy min **5m**; Healthy merge **<=2m**; public default **7d**; retention **90d**. Order: detector+entity → aggregator+snapshot → windows API → public UI+visuals → admin suppress/annotate.

