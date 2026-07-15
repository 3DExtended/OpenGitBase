<!-- forge: #6 -->

# PRD: Admin Replication UI

## Problem Statement

OpenGitBase now replicates every repository across three storage nodes (RF=3) with background backfill for legacy single-node assignments. Operators need to answer operational questions without reading logs or calling raw admin API endpoints: Is the fleet eligible for RF=3? Which repositories are still backfilling? Which replicas are lagging? Can this repo accept quorum writes?

The HA storage work shipped admin replication **read APIs** (`GET /admin/storage-nodes/replication-summary` and `GET /admin/repositories/{id}/replication`) and a placeholder note on the storage admin page, but **no visual surfaces** consume them. An operator on production cannot see backfill progress, replication state, or watermark lag in the web UI today.

## Solution

Deliver a complete admin replication experience in one release:

1. **Extend `/admin/storage`** with a fleet replication card: enhanced node role counts, fleet health gate, aggregate state rollup, and an attention teaser for repos needing operator review.
2. **Add `/admin/repositories`** as a paginated, filterable, searchable index of all repositories with inline replication summary and derived backfill/sync progress indicators.
3. **Add `/admin/repositories/{id}`** as a per-repository drill-down with status header, visual primary-vs-replica layout, and replica-level watermark detail.
4. **Add a second tile on the admin home page** (“Replication”) alongside the existing Storage tile.

All three surfaces auto-refresh every 30 seconds while mounted. Filter and attention rules are shared between the storage-page teaser and the repository index so operators see consistent “what needs attention” semantics everywhere.

## User Stories

### Fleet visibility (storage admin page)

1. As an operator, I want to see how many storage nodes are healthy and whether the fleet meets the RF=3 minimum (three healthy nodes), so that I know whether new repository creation is allowed.
2. As an operator, I want each storage node card to show how many repositories it serves as primary and as replica, plus whether the node is spare capacity, so that I can assess load distribution without querying the database.
3. As an operator, I want a fleet-wide rollup of repository replication states (counts per `ReplicationState`), so that I can gauge overall migration and health at a glance.
4. As an operator, I want a short list of repositories that need attention on the storage page, so that I can jump to problems without opening the full index first.
5. As an operator, I want attention repos sorted by severity (no write quorum first, then degraded/promoting, then backfilling, then lagging), so that the most urgent issues appear first.
6. As an operator, I want a “View all repositories” link from the storage page teaser to the filtered repository index, so that I can continue investigation in one click.
7. As an operator, I want the fleet replication card to refresh automatically every 30 seconds, so that node and rollup status stays current during an incident.

### Repository index

8. As an operator, I want a dedicated admin page listing all repositories with replication summary fields, so that I can audit the fleet without knowing repository GUIDs in advance.
9. As an operator, I want the repository index paginated server-side (default page size 50), so that the UI remains responsive as repository count grows.
10. As an operator, I want the index sorted by severity by default (problems first), so that backfilling and degraded repos surface immediately.
11. As an operator, I want to sort the index by name, watermark lag, or replication state, so that I can switch between operational and alphabetical views.
12. As an operator, I want filter chips (All, Backfilling, Degraded, Lagging, No quorum) that query the server, so that filtering applies across the full dataset—not just the current page.
13. As an operator, I want to search repositories by name or owner slug via the server, so that I can find a specific repo in a large fleet.
14. As an operator, I want each index row to show repository name, owner, replication state badge, replica count (e.g. 2/3), primary node id, max watermark lag, write-quorum availability, oldest last-synced timestamp, and replication epoch, so that I can triage without opening detail.
15. As an operator, I want a provisioning progress bar derived from replica count (replicas / 3), so that I can see how far RF=3 placement has progressed during backfill.
16. As an operator, I want a sync progress bar derived from watermark lag across replicas, so that I can see how far data catch-up has progressed even when `ReplicationState` already reads healthy.
17. As an operator, I want the repository index to auto-refresh every 30 seconds, so that backfill progress updates while I watch the page.
18. As an operator, I want pagination controls with total count, so that I know how many repositories match the current filter.

### Repository detail

19. As an operator, I want a per-repository admin page showing name, owner, and a link to the public repository page, so that I have context while debugging replication.
20. As an operator, I want the detail page to show replication state, epoch, primary watermark, and write-quorum availability, so that I understand the repo’s control-plane status.
21. As an operator, I want replicas displayed in a visual layout that highlights the primary node versus secondary replicas, so that role assignment is obvious during promotion or rebalance events.
22. As an operator, I want each replica row to show storage node id (human-readable, e.g. `storage-1`), role, applied watermark, in-sync flag, last synced time, and lag delta (primary watermark minus applied watermark), so that I can pinpoint stale copies.
23. As an operator, I want the detail page to auto-refresh every 30 seconds, so that I can watch a single repo complete backfill or catch-up.
24. As an operator, I want the detail page to render correctly for pre-backfill and mid-backfill repositories (fewer than three replicas, zero watermarks), so that legacy migration states do not break the UI.

### Navigation and discoverability

25. As an operator, I want a “Replication” tile on the admin home page separate from “Storage”, so that repository health and node provisioning are distinct entry points.
26. As an operator, I want the Storage tile to remain focused on nodes, enrollments, and fleet SSH keys, so that provisioning workflows are not cluttered by repository tables.

### API and security

27. As the API, I want a new admin-only list endpoint returning paginated repository replication summaries, so that the web UI does not perform N+1 calls to per-repository detail endpoints.
28. As the API, I want attention filter presets on the list endpoint to use the same rules as the storage-page teaser, so that “needs attention” is defined once server-side.
29. As a security reviewer, I want all new replication admin endpoints restricted to the admin role, consistent with existing admin storage endpoints.
30. As a tester, I want API tests covering list pagination, attention filters, severity sort, and search, so that operator-facing aggregation logic is regression-protected.

### Derived progress (no new job queue)

31. As an operator, I want backfill progress inferred from existing fields (replica count, watermarks, replication state)—not from a background-job queue—so that progress is visible in this release without new worker instrumentation.
32. As an operator, I want progress bars to distinguish **provisioning** (how many of three replica placements exist) from **sync** (how caught-up replicas are on watermarks), so that I can tell “missing copy” apart from “copy exists but is stale”.

## Implementation Decisions

### Scope and surfaces

- Ship **both** fleet-level (storage page extension) and repository-level (index + detail) surfaces in **one release**.
- Admin home gets **two tiles**: existing Storage, new Replication → `/admin/repositories`.
- Remove placeholder raw API path hints from the storage page once real UI is wired.

### New admin list endpoint

Add `GET /admin/repositories` (admin role required) returning a paginated list of replication summaries.

**Summary fields per repository:**

| Field | Purpose |
|-------|---------|
| `repositoryId` | Key for detail page |
| `name` | Display name |
| `ownerSlug` | Owner namespace for display and search |
| `replicationState` | Enum string: `Rf1Backfilling`, `Rf3Healthy`, `Degraded`, `Promoting` |
| `replicaCount` | Number of replica-set members (0–3+) |
| `primaryNodeId` | Human-readable node id (e.g. `storage-1`), resolved from primary storage node |
| `primaryWatermark` | Current primary watermark |
| `maxWatermarkLag` | Max of `(primaryWatermark - appliedWatermark)` across replicas |
| `writeQuorumAvailable` | Whether quorum writes are currently possible |
| `replicationEpoch` | Current epoch/generation |
| `oldestLastSyncedAt` | Minimum `LastSyncedAt` across replicas (nullable) |

**Response envelope:**

```
{
  items: [...],
  totalCount: number,
  page: number,
  pageSize: number
}
```

**Query parameters:**

| Param | Behavior |
|-------|----------|
| `page` | 1-based page index (default 1) |
| `pageSize` | Page size (default 50, capped e.g. at 100) |
| `sort` | `severity` (default), `name`, `lag`, `state` |
| `search` | Case-insensitive match on repository name and owner slug |
| `attention` | Preset filter: `all` (default), `backfilling`, `degraded`, `lagging`, `no-quorum` |

**Attention preset rules** (shared with storage-page teaser):

A repository “needs attention” when **any** of:

- `replicationState` is not `Rf3Healthy`
- `writeQuorumAvailable` is false
- `maxWatermarkLag` > 0
- `replicaCount` < 3

Preset mapping:

- `backfilling` → `replicationState == Rf1Backfilling`
- `degraded` → `replicationState == Degraded` or `Promoting`
- `lagging` → `maxWatermarkLag > 0`
- `no-quorum` → `writeQuorumAvailable == false`

**Severity sort order** (for default `sort=severity`):

1. No write quorum
2. Degraded / Promoting
3. Backfilling
4. Lagging (`maxWatermarkLag > 0`) while state is otherwise healthy
5. Fully healthy

### Deep module: repository replication list query

Extract list logic into a dedicated query handler (CQRS) rather than embedding LINQ in the controller. This module encapsulates:

- Joining repositories with replica rows and storage nodes for summary projection
- Computing `maxWatermarkLag`, `oldestLastSyncedAt`, and `writeQuorumAvailable` (reuse existing routing/quorum logic where possible)
- Applying attention presets and search
- Applying severity or alternate sorts
- Pagination with stable total count

**Interface shape (conceptual):**

```
ListAdminRepositoryReplicationQuery {
  Page, PageSize, Sort, Search, Attention
}
→ ListAdminRepositoryReplicationResult {
  Items[], TotalCount, Page, PageSize
}
```

This is the primary deep module: rich behavior, simple controller surface, testable in isolation with seeded repositories and replica sets.

### Deep module: attention matcher

Extract attention predicate and severity rank into a small shared helper used by:

- List query handler (`attention` query param)
- Optional fleet rollup / teaser endpoint or client-side filter of first page

Keeps teaser chips and index filters aligned without duplicating rules in the web app.

### Existing endpoints (consume, minimal change)

- **`GET /admin/storage-nodes/replication-summary`** — unchanged; storage page loads this for node cards and spare indicator.
- **`GET /admin/repositories/{id}/replication`** — unchanged for detail page; optionally extend replica DTO with human-readable `nodeId` string to avoid client-side GUID joins (implementation choice; client-side join via storage node list is acceptable fallback).

Fleet rollup counts (e.g. “12 RF=3 healthy · 2 backfilling”) may be computed either:

- **Client-side** from a lightweight aggregate call or first page of list with `pageSize=0`/summary-only extension, **or**
- **Server-side** as an optional field on `replication-summary` or a small `GET /admin/repositories/replication-rollup` endpoint.

**Assumption:** Client-side rollup from list endpoint totals or a dedicated count query in the list handler is acceptable for v1; avoid N+1.

### Web application modules

| Module | Change |
|--------|--------|
| Admin home | Add Replication tile linking to `/admin/repositories` |
| Storage admin page | Add fleet replication card (gate, enhanced nodes, rollup, teaser, 30s refresh); remove API path placeholder text |
| Repository index page | New page: table, filter chips, search, pagination, progress bars, 30s refresh |
| Repository detail page | New page: header, status, visual replica layout, 30s refresh |
| API client | Add typed methods for list, replication summary, and per-repo detail |
| i18n | Add strings for replication states, column headers, filter labels, empty states, fleet gate messages |

### UI behavior details

**Fleet gate banner:** Display `{healthyCount}/3` healthy nodes and whether RF=3 creation is eligible (three healthy nodes registered).

**Attention teaser:** Show at most **5** repos matching attention rules, severity-sorted; link “View all →” to `/admin/repositories?attention=…` (appropriate preset when launched from a specific rollup context, or `all` attention view).

**Progress bars (derived, no new backend job fields):**

- **Provisioning bar:** `replicaCount / 3` (cap display at 100% when ≥ 3)
- **Sync bar:** derive from watermark lag—e.g. in-sync when all replicas match primary watermark; partial progress when lag > 0 (exact visual mapping left to UI implementation; must monotonically improve as lag decreases)

**Auto-refresh:** 30-second interval on storage fleet card, repository index, and repository detail; clear interval on unmount; manual Refresh button retained.

### Schema changes

None. All data comes from existing `Repository`, `RepositoryReplica`, and `StorageNode` entities plus existing routing/quorum queries.

### Architectural decisions

- **Server-side pagination and filtering** (not client-side load-all)—operator chose paginated API even though current fleet is small.
- **No background-job progress API** in this release—progress is derived from replica count and watermarks only.
- **Separate admin tiles** for Storage vs Replication—provisioning and observability stay distinct.
- **Attention semantics defined server-side**—web app does not re-implement filter rules for presets.

## Testing Decisions

### What makes a good test

Test **observable behavior** at module boundaries: given seeded repositories with varied replica sets, states, and watermarks, assert the list endpoint returns correct items, counts, sort order, and filter results. Do not test internal LINQ structure or private helpers directly—test through the query handler or controller HTTP surface.

### Modules to test

| Module | Test focus |
|--------|------------|
| List query handler | Pagination boundaries; each `attention` preset; `search` matching; `sort=severity` ordering; summary field computation (`maxWatermarkLag`, `oldestLastSyncedAt`, `primaryNodeId`) |
| Attention matcher | Predicate true/false for edge cases: healthy RF=3, backfilling with 2 replicas, healthy state with lagging replica, no quorum |
| Controller (integration) | Admin auth required; 200 shape; empty fleet |
| Existing detail endpoint | Regression: pre-backfill repo with < 3 replicas still returns 200 (already partially covered) |

### Prior art

- `AdminRepositoryReplicationControllerTests` — controller + seeded EF context pattern
- `CreateRepositoryWithStorageQueryHandlerTests`, `Rf1BackfillServiceTests` — replica set seeding conventions
- Admin storage page tests (if any Playwright/shell tests exist)—optional UI smoke; API tests are primary for this PRD

### UI testing

Manual or Playwright smoke optional for v1. API tests carry the correctness burden for attention rules and severity sort. If UI tests are added, limit to “page renders with seeded API mock” rather than testing sort implementation twice.

## Out of Scope

- Background job queue or explicit `backfillProgress` percentage from `Rf1BackfillService` / `RebalanceService` / `AntiEntropyReconcilerService`
- Raw JSON toggle or support-only diagnostics panel on detail page
- Client-side-only pagination, filtering, or search
- Editing replication state, forcing promotion, or triggering backfill from the UI (read-only observability)
- Non-admin repository replication views (owner-facing repo settings)
- Alerts, notifications, or Prometheus metrics export (future observability stack)
- Separate `/admin/replication` route—the index lives at `/admin/repositories` per navigation decision

## Further Notes

### Relationship to HA storage PRD

This PRD completes **user stories 31 and 33** from the HA storage replication PRD and supersedes the partial delivery of issue `ha-storage-11`, which shipped APIs and placeholder text only.

### Current production context

Production runs three storage nodes with RF=3 backfill active. Operators currently see replication state only via direct API calls or database queries. This PRD unblocks day-two operations after HA deployment.

### Assumptions

- Admin authentication and `admin` role middleware already gate `/admin/*` routes; new endpoints follow the same pattern.
- Owner slug for display/search is resolved via existing repository ownership relations (user or organization slug).
- `writeQuorumAvailable` on list rows uses the same logic as `RepositoryReplicationRoutingQuery` on the detail endpoint for consistency.
- English i18n strings are sufficient for v1; locale keys structured for future translation.
- OpenAPI/codegen for the web client may be updated manually in `api.ts` if codegen is not wired for admin routes today.

### Open implementation choices (non-blocking)

- Fleet rollup: dedicated count query vs. aggregate from list handler vs. client-side from summary endpoint extension.
- Replica `nodeId` on detail API response vs. client-side join using cached storage node list.
- Exact visual formula for sync progress bar when watermarks are zero during initial backfill (show indeterminate or 0% until first watermark commit).
