<!-- forge: #23 -->

# PRD: Public Status Dashboard

## Problem Statement

OpenGitBase runs a multi-component distributed stack—web UI, API control plane, git dispatchers, RF=3 storage fleet, Postgres, and Redis—but operators and users have no single place to see whether the forge is healthy. The existing `GET /health` endpoint reports only the local API instance (application + database connectivity). Fleet visibility lives behind admin-only routes (`/admin/storage`) and internal registration APIs. During incidents, users cannot tell whether git push failures, slow browsing, or storage degradation are platform-wide or isolated; operators must read logs or call raw admin endpoints.

The project needs a **public status page** comparable to common forge/hosting status sites: current component health with named instances, recent uptime history, and a place for operators to broadcast a short incident message—without exposing internal network topology, enrollment secrets, or per-repository replication detail.

## Solution

Deliver a hybrid status experience:

1. **Public `/status` page** (anonymous, footer-linked) showing five collapsible component groups—Website, API, Git, Storage, Data stores—each expandable to named instances with status badges and probe metadata. Groups that are Degraded or Unhealthy auto-expand on load. The page auto-refreshes every 30 seconds.
2. **Background status aggregator** on the control plane: one active worker (Postgres advisory lock) probes all registered components every 30 seconds, rolls up group and overall status, and persists hourly history buckets for 90-day charts.
3. **Auto-discovery via self-registration**: API, web, and git dispatcher instances register and heartbeat on startup (mirroring the existing storage-node registry pattern). Storage instances continue to use the existing storage-node registry. Postgres and Redis probe targets are derived from existing connection configuration—no manual probe target lists in compose.
4. **90-day history charts**: multi-line daily uptime % per group (with toggles) plus a stacked overall Healthy/Degraded/Unhealthy chart—backed by cheap hourly aggregates in Postgres, not raw 30-second samples.
5. **Admin incident banner** at `/admin/status`: operators post a short message with severity; it appears at the top of the public page until resolved. Admin drill-down for fleet replication remains at `/admin/storage`.

Public read APIs redact internal hosts, ports, certificate thumbprints, and disk usage. Admin surfaces are unchanged for deep replication observability.

## User Stories

### Public visibility

1. As a visitor, I want a `/status` page I can open without signing in, so that I can see whether the forge is operational before reporting an issue.
2. As a visitor, I want an overall status indicator (Healthy / Degraded / Unhealthy) at the top of the page, so that I can assess the platform in one glance.
3. As a visitor, I want component health grouped into Website, API, Git, Storage, and Data stores, so that I can tell which layer is affected during an incident.
4. As a visitor, I want each group collapsible with named instances inside (e.g. `web-1`, `api-2`, `storage-3`), so that I can see per-instance state without admin access.
5. As a visitor, I want Degraded or Unhealthy groups expanded automatically on page load, so that problems are visible immediately without clicking.
6. As a visitor, I want Healthy groups collapsed by default, so that the page stays readable when everything is fine.
7. As a visitor, I want each instance row to show status, last checked time, response time (where probed), last seen (storage heartbeats), and a short message when relevant, so that I have enough context to understand a failure.
8. As a visitor, I want the page to refresh automatically every 30 seconds, so that status stays current during an ongoing incident.
9. As a visitor, I want a footer link to System status on all layouts, so that I can find the page from anywhere on the site.
10. As a visitor, I want a 90-day uptime chart with an overall line and the ability to toggle individual component groups, so that I can see historical reliability trends.
11. As a visitor, I want a second chart showing the daily mix of Healthy, Degraded, and Unhealthy time for the overall platform, so that I understand incident severity patterns—not just binary up/down.
12. As a visitor, I want history charts to show partial data gracefully when the system has been running for less than 90 days, so that a new deployment does not show a broken chart.
13. As a visitor, I want an operator incident banner at the top of the page when one is active, so that I know the team is aware of a known issue.
14. As a security-conscious operator, I want the public page to omit internal hostnames, ports, certificate thumbprints, enrollment tokens, and disk byte counts, so that the status page does not leak infrastructure topology.

### Status semantics

15. As a visitor, I want the Storage group to show Healthy when 3/3 storage nodes are healthy, Degraded when 2/3 are healthy, and Unhealthy when 0–1/3 are healthy, so that RF=3 margin is reflected accurately.
16. As a visitor, I want individual storage nodes marked Degraded when heartbeats are stale (beyond threshold) or probes are slow, and Unhealthy when heartbeats are missing or probes fail, so that node-level state matches operator expectations.
17. As a visitor, I want API, web, and git instances marked Unhealthy on probe failure or timeout, Degraded when probes succeed but exceed a slow threshold, and Healthy otherwise, so that latency regressions surface before hard failure.
18. As a visitor, I want Postgres and Redis marked Unhealthy when connectivity fails and Degraded when connectivity succeeds but is slow, so that data-store issues are visible separately from application layers.
19. As a visitor, I want each group's status derived from its worst child (Unhealthy beats Degraded beats Healthy), so that rollup rules are predictable.
20. As a visitor, I want overall page status derived from the worst group status, so that one failing layer marks the platform Unhealthy.

### Auto-discovery and registration

21. As an API instance, I want to register myself with instance id and internal probe URL on startup and send periodic heartbeats, so that I appear on the status page without manual compose configuration.
22. As a web instance, I want to register and heartbeat on startup, so that scaling web replicas automatically updates the status dashboard.
23. As a git dispatcher instance, I want to register and heartbeat on startup, so that dispatcher fleet changes are reflected automatically.
24. As a storage node, I want to continue using the existing storage-node registration and heartbeat flow, so that storage discovery does not require a parallel registry.
25. As the status aggregator, I want Postgres and Redis probe targets parsed from existing SQL and Redis connection configuration, so that data stores appear without extra operator config.
26. As the status aggregator, I want stale registrations removed or marked absent when heartbeats stop (TTL-based), so that decommissioned instances do not linger as falsely healthy.
27. As an operator deploying to non-compose environments, I want an optional configuration override for probe targets when self-registration is unavailable, so that exotic deployments remain supportable.

### Background aggregation

28. As the platform, I want exactly one active status aggregator at a time using a Postgres advisory lock, so that probes and history writes are not duplicated across API replicas.
29. As the platform, I want the aggregator to fail over automatically to another API instance when the lock holder stops, so that monitoring continues during api-1 failure.
30. As the platform, I want probes to run every 30 seconds, so that public status and admin observability share a consistent freshness cadence with the existing admin storage refresh interval.
31. As the platform, I want probe results stored as an in-memory or short-lived snapshot readable by the public API, so that page loads are fast and probe cost is bounded.
32. As the platform, I want hourly history buckets persisted per component group with counts of Healthy, Degraded, and Unhealthy sample minutes, so that 90-day charts are cheap to store and query.
33. As the platform, I want history retention of at least 90 days with automatic pruning of older buckets, so that database growth stays bounded.

### Public API

34. As the web UI, I want an anonymous `GET` endpoint returning the current public status snapshot including groups, instances, overall status, timestamp, and active incident banner, so that the status page does not require authentication.
35. As the web UI, I want an anonymous `GET` endpoint returning 90 days of daily rollup history for charts, so that graph data is server-authoritative.
36. As a security reviewer, I want component registration and heartbeat endpoints restricted to internal networks (same posture as storage-node registration), so that external clients cannot inject fake fleet members.
37. As a security reviewer, I want the public status endpoints rate-limited appropriately, so that the anonymous surface cannot be abused for DoS amplification.

### Admin incident banner

38. As an admin, I want an `/admin/status` page where I can compose an incident banner with message and severity (info / warning / outage), so that I can communicate known issues to users quickly.
39. As an admin, I want to resolve (clear) the active banner with one action, so that the public page returns to normal when the incident ends.
40. As an admin, I want at most one active banner at a time, so that messaging stays focused during an incident.
41. As an admin, I want a preview or link to the public `/status` page from the admin page, so that I can verify what users see.
42. As an admin, I want a link from the public status page to admin fleet tools when I am signed in as admin, so that I can drill down into replication detail during an incident.
43. As a security reviewer, I want incident banner management restricted to the admin role, so that only operators can post public messages.

### Application health endpoints

44. As the status aggregator, I want git dispatcher instances to expose a lightweight health HTTP endpoint, so that probes have a stable target beyond implicit TCP checks.
45. As the status aggregator, I want web instances to expose or accept probes against a lightweight health path, so that web health is distinguishable from generic TCP reachability.
46. As the status aggregator, I want API instances probed via the existing health endpoint, so that API health reuse stays consistent.

### Navigation and routing

47. As the web app, I want `/status` reserved as a top-level route (not colliding with owner/repo slugs), so that the URL is stable and discoverable.
48. As the web app, I want the status page usable by guests with the standard site header (no sidebar required), so that the page feels part of the product without forcing sign-in.
49. As an operator, I want `/admin/storage` to remain the deep fleet and replication drill-down, so that the public page does not replace admin observability.

### Operations

50. As an operator, I want the status aggregator to log probe failures and lock ownership changes at info/warn levels, so that incident debugging remains possible via API logs.
51. As an operator, I want public status to reflect storage heartbeats already maintained by the control plane, so that storage health aligns with RF=3 eligibility logic elsewhere in the system.

## Implementation Decisions

### Scope and surfaces

- Ship **public `/status`**, **public read APIs**, **background aggregator**, **self-registration for API/web/git**, **90-day history**, and **admin incident banner** in one release.
- **Hybrid model**: public summary + existing `/admin/storage` for replication drill-down; no duplication of per-repository replication on the public page.
- Reserve the `status` slug alongside existing reserved routes (`pitch`, `health`, etc.).

### Deep modules (testable boundaries)

| Module | Responsibility | Interface (conceptual) |
|--------|----------------|----------------------|
| **Fleet component registry** | Persist registered instances by component type; accept heartbeats; mark stale entries absent after TTL | Register, heartbeat, list active by group, purge stale |
| **Status probe engine** | Given probe targets, execute HTTP/TCP checks with timeout; return per-instance probe results with duration and message | ProbeAll(targets, timeout) → instance results |
| **Status rollup engine** | Apply Healthy/Degraded/Unhealthy rules per instance type; roll up group and overall status | Rollup(instance results, storage heartbeats) → group tree + overall |
| **Status snapshot store** | Hold latest public snapshot written by aggregator; serve reads for public API | WriteSnapshot, ReadSnapshot |
| **Status history aggregator** | Upsert hourly buckets from periodic snapshots; roll daily points for chart API; prune >90 days | RecordSample, GetDailyHistory(90d) |
| **Advisory lock leader election** | Ensure one aggregator worker; release on shutdown; retry on other API instances | TryAcquire, Renew, Release |
| **Incident banner service** | CRUD for single active banner (admin); include in public snapshot | GetActive, Set, Resolve |
| **Public status projection** | Map internal registry + probe data to redacted public DTO | BuildPublicSnapshot |

These modules should be testable without HTTP or EF where possible (rollup and history math in isolation; registry via in-memory or test DB).

### Component groups and discovery

Five public groups with fixed enum ordering:

1. **Website** — self-registered web instances
2. **Api** — self-registered API instances
3. **Git** — self-registered git dispatcher instances
4. **Storage** — existing storage-node registry (not a second registration path)
5. **Data stores** — postgres and redis derived from connection strings

Storage group rollup uses healthy node count against RF=3 thresholds (3 = Healthy, 2 = Degraded, 0–1 = Unhealthy). Individual storage rows use existing `IsHealthy` and `LastHeartbeatAt` plus optional HTTP probe where applicable.

Postgres probe: connect/check via existing data access stack. Redis probe: connect/ping via existing Redis client configuration.

### Self-registration contract

New internal endpoints (internal-network restricted):

- Register: instance id, component type, internal probe URL, optional metadata version/build
- Heartbeat: instance id, timestamp

Registration upserts by `(componentType, instanceId)`. Heartbeat TTL default **90 seconds**—instances without heartbeat marked **Unhealthy** with message "Heartbeat stale" or absent from active set per implementation choice (prefer marking Unhealthy rather than hiding silently).

Optional `StatusProbe__Targets` configuration override for environments where self-registration is impractical; not the primary path.

Each registering application starts a background heartbeat loop (same interval order of magnitude as storage nodes).

### Aggregator background service

- New hosted background service on the API, modeled after existing HA storage background services.
- On each tick (~30s): acquire/renew advisory lock → load active fleet → run probes in parallel with timeout → merge storage registry → rollup → write snapshot → update hourly history bucket → release or hold lock.
- Only the lock holder executes probes and history writes.
- Probe timeout default **5 seconds** per instance; slow threshold **2 seconds** for Degraded on HTTP components.

### Status semantics (normative)

| Target | Healthy | Degraded | Unhealthy |
|--------|---------|----------|-----------|
| Storage fleet group | 3/3 nodes healthy | 2/3 healthy | 0–1/3 healthy |
| Storage instance | Fresh heartbeat + probe OK | Stale heartbeat (>90s) or slow probe | Missing heartbeat or probe failure |
| API / Web / Git instance | Probe OK, duration < 2s | Probe OK, duration 2–5s | Probe fail or timeout |
| Postgres / Redis | Connect OK, fast | Connect OK, slow | Connect fail |
| Group | Best-effort all children Healthy | Any Degraded, none Unhealthy | Any Unhealthy |
| Overall | All groups Healthy | Any Degraded, none Unhealthy | Any Unhealthy |

### Public DTO (redaction)

Public instance fields:

- `instanceId`, `group`, `status`, `lastCheckedAt`, `responseTimeMs` (nullable), `lastSeenAt` (nullable, storage), `message` (nullable)

Excluded from public responses:

- `internalHost`, ports, certificate thumbprint, enrollment tokens, free/total bytes, disk percentages, replication summaries

Public snapshot also includes: `overallStatus`, `checkedAt`, `groups[]`, `incident | null`.

### History storage

New table(s) for hourly buckets per group:

```
HourlyBucket {
  group, periodStartUtc,
  healthySamples, degradedSamples, unhealthySamples, totalSamples
}
```

Aggregator increments the current hour bucket each probe cycle based on rolled-up group status. Chart API returns **daily** series for 90 days (aggregate 24 hourly buckets per day). Uptime % = `(healthySamples + degradedSamples * weight) / total` — default weight 0.5 for degraded in uptime line unless implementation chooses strict healthy-only; document chosen formula in implementation and test it.

Stacked chart uses daily ratios of Healthy / Degraded / Unhealthy sample counts for **overall** status only.

Retention: delete buckets older than 90 days (daily job or inline prune).

Storage estimate: ~5 groups × 24 × 90 ≈ 11k rows — negligible.

### Public API endpoints

| Endpoint | Auth | Purpose |
|----------|------|---------|
| `GET /api/v1/public/status` | Anonymous | Current snapshot + incident |
| `GET /api/v1/public/status/history?days=90` | Anonymous | Daily chart series |

Existing `GET /health` remains per-instance liveness for load balancers; unchanged semantics.

Internal registration under `/api/v1/internal/fleet-components/` (or consistent internal prefix) added to `InternalNetworkOptions.RestrictedPathPrefixes`.

### Admin incident banner

- New admin page `/admin/status` with form: message (plain text, max ~500 chars), severity enum (`info`, `warning`, `outage`), submit and resolve actions.
- Persist single active row or active flag; resolving clears public incident field.
- No incident history log in this release (resolve = delete/deactivate, not archive).

Admin tile or link from admin home to `/admin/status` (alongside Storage / Replication entry points).

### Web UI

**`/status` page:**

- Overall status badge and last updated timestamp
- Incident banner when present (severity-driven styling)
- Collapsible groups; auto-expand non-Healthy
- Instance tables per group
- Two charts (90-day multi-line uptime with group toggles; stacked overall state mix)
- 30s polling of public status + history endpoints
- Admin cross-link when authenticated admin

**Global footer:**

- Add site footer component to default layout with "System status" link (i18n key under `footer`)

**`/admin/status` page:**

- Incident banner editor
- Link to public `/status` and `/admin/storage`

Chart library: no existing chart dependency in the web app; introduce a lightweight chart library suitable for Nuxt SSR/client (implementation choice—prefer one dependency, accessible colors, responsive layout).

### Schema changes

- `FleetComponent` entity (or equivalent): type, instance id, probe url, registered at, last heartbeat at, optional version
- `StatusIncidentBanner` entity: message, severity, active flag, created/updated timestamps, created by admin user id
- `StatusHistoryHourlyBucket` entity: as above

Migration via standard EF Core migration pattern.

### Architectural decisions

- **Self-registration over static compose probe lists** as primary discovery; config override as escape hatch.
- **Reuse storage-node registry** for Storage group—do not duplicate storage registration.
- **Postgres advisory lock** for aggregator leadership—not hardcoded api-1 only.
- **Hourly aggregates, not raw 30s samples**, for 90-day history cost control.
- **Public read-only** status surface; registration internal-only.
- **No disk usage on public page** even though storage nodes track bytes internally.
- **Incident banner minimal model**: message + severity + resolve; no affected-group checkboxes in v1.

### Assumptions

- English i18n strings sufficient for v1; keys structured for future translation.
- OpenAPI/codegen updated for new public and admin endpoints; web client extended manually if codegen lagging.
- Guest users see standard header without sidebar (existing layout behavior for unauthenticated non-repo routes).
- Storage heartbeat thresholds align with existing storage-node health logic where possible to avoid contradictory signals.
- Chart displays "insufficient history" or partial line for deployments younger than 90 days rather than failing.

## Testing Decisions

### What makes a good test

Test **observable behavior at module boundaries**: given seeded fleet registrations, probe responses (mocked HTTP/TCP), and storage node heartbeats, assert rollup output, public DTO redaction, history bucket math, and HTTP status codes on endpoints. Do not test private timer loops directly—test the rollup, snapshot builder, and query handlers through their public interfaces. For the background service, test the "single tick" orchestration with mocked dependencies.

### Modules to test

| Module | Test focus |
|--------|------------|
| Status rollup engine | Storage 3/2/1 healthy group rules; worst-child group rollup; overall rollup; Degraded thresholds for slow probes |
| Public status projection | Redaction: no internal host, ports, certs, disk fields; correct field presence |
| Fleet component registry | Register upsert; heartbeat updates; stale TTL marks Unhealthy or excludes |
| Status history aggregator | Hourly upsert increments; daily rollup for charts; uptime percentage formula |
| Advisory lock wrapper | Second acquirer fails while held; release allows takeover (integration with Postgres or test double) |
| Incident banner service | Set active replaces previous; resolve clears; admin-only mutations |
| Public status controller | Anonymous 200; snapshot shape; rate limit not blocking normal use |
| Internal registration controller | External IP → 403; internal IP → 200 |
| Health endpoint additions | Dispatcher/web `/health` returns 200 when running |

### Prior art

- `SystemHealthCheckQueryHandlerTests` — probe result structure and timeout handling
- `HealthControllerTests` — anonymous health endpoint patterns
- `InternalNetworkMiddlewareIntegrationTests` — restricted path 403 from external clients
- `HaStorageBackgroundService` / background service testing patterns in API tests
- `ListStorageNodeQueryHandlerTests` — storage fleet seeding conventions
- `InfrastructureSmokeTests.ApiHealthReturnsHealthy` — compose-tier smoke; add optional Tier0 public status smoke when compose stack includes registered components
- Admin controller tests — admin auth required pattern from replication admin tests

### UI testing

- Manual or Playwright smoke: `/status` renders with mocked API; footer link navigates; collapsible groups expand when Degraded.
- Visual snapshot optional for status page layout; API tests carry correctness for rollup and redaction.

## Out of Scope

- Full incident log with timestamps, resolved history, and postmortems (option C from design discussion)
- Affected-component checkboxes on incident banner
- Public disk usage or capacity metrics for storage nodes
- Per-repository replication status on the public page
- Prometheus metrics export, PagerDuty, or external alerting integrations
- Docker-socket or compose-label auto-discovery (self-registration is the chosen model)
- Replacing or removing existing admin storage/replication UI
- Owner-facing or repo-scoped health views
- Manual editing of probe results or "maintenance mode" suppression of groups
- Multi-language incident banners
- Sub-hour zoom on history charts (daily display only for 90-day window)
- Synthesized/historical backfill of uptime data from before this feature ships

## Further Notes

### Relationship to existing health and admin surfaces

- `GET /health` remains the load-balancer liveness probe for individual API containers.
- `/admin/storage` and replication admin pages remain the operator drill-down for RF=3 backfill, watermarks, and enrollments.
- Storage nodes continue registering via existing `/api/v1/storage-nodes/register` and heartbeat flows; the status aggregator reads that registry rather than re-implementing storage enrollment.

### Cost and performance

- ~30s probe cycle across ~10–15 instances with 5s timeout cap is bounded work for one API leader.
- Hourly history buckets keep 90-day storage under ~11k rows.
- Public page polls two lightweight read endpoints every 30s—acceptable for anonymous traffic with rate limiting.

### Open implementation choices (non-blocking)

- Exact uptime formula weight for Degraded minutes in the line chart (0.5 vs 0 vs counted as healthy).
- Whether stale self-registered instances appear as Unhealthy rows or are hidden with a fleet count note—recommend **show as Unhealthy** for transparency.
- Chart library selection for Nuxt (Chart.js, Unovis, ECharts, etc.).
- Whether `/admin/status` gets its own admin home tile or a link from the Storage admin page only.
- Optional `StatusProbe__Enabled` master switch to disable aggregator in test environments without Postgres lock noise.

### Design session reference

Requirements captured from interactive design review (2026-07-10): hybrid public/admin model, collapsible named instances, 30s aggregator with advisory lock, self-registration discovery, five component groups, 90-day dual charts, minimal admin incident banner, no public disk metrics.
