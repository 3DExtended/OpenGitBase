# PRD: Repository Web Browsing (File Tree, Blob View, README)

## Problem Statement

OpenGitBase today exposes repository metadata and clone/push instructions in the web UI, but users cannot browse repository contents in the browser. There is no file tree, no file viewer, and no rendered README on the repository home page. Git operations (clone, fetch, push) work through the dispatcher and storage layer, yet the product lacks the basic “forge” experience developers expect when visiting a repository URL.

Users who can read a repository via git should be able to read the same content through the website — with appropriate authorization for private repositories and open access for public ones. Operators also need web browsing to avoid adding read load to primary storage nodes, which must remain available for git write/read protocol traffic.

## Solution

Add **repository web browsing**: a GitHub-style code view in the Nuxt web app backed by new API endpoints and internal storage-node content APIs. The repository home page shows a root directory listing and a rendered README (tree first, README below). Users navigate folders and files via `/tree/{ref}/{path}` and `/blob/{ref}/{path}` routes with a ref picker supporting branches and tags.

The API enforces read access (anonymous for public repos; session auth for private repos), selects a **healthy non-primary replica** for content reads (never the primary), and caches responses in **Redis** with short TTL plus `Cache-Control` headers for public content. Replicas may lag briefly behind the primary; when replication watermark metadata indicates lag, the UI shows a subtle “syncing…” banner while still serving replica data.

## User Stories

### Public and private access

1. As an anonymous visitor, I want to browse files in a **public** repository, so that I can evaluate projects without creating an account.
2. As an anonymous visitor, I want to see a rendered README on a public repository home page, so that I understand what the project is about.
3. As an anonymous visitor attempting to view a **private** repository, I want a **404 Not Found** response, so that private repositories are not revealed to exist.
4. As a signed-in user without repository access, I want a **403 Forbidden** response when viewing a private repository, so that I know the repository exists but I lack permission.
5. As a signed-in repository member (or org member with read access, or owner), I want to browse a private repository’s files, so that I can inspect code without cloning.
6. As a security reviewer, I want web content authorization to reuse the same membership and organization role rules as git read access, so that permissions stay consistent across transports.
7. As a security reviewer, I want private repository content responses marked `no-store`, so that shared caches and browsers do not retain unauthorized data.

### Repository home and navigation

8. As a repository visitor with read access, I want the home page (`/{owner}/{repo}`) to show the root file tree, so that I can immediately see project structure.
9. As a repository visitor, I want the README rendered **below** the file tree on the home page, so that structure is visible before documentation.
10. As a repository visitor, I want to open folders via `/tree/{ref}/{path}`, so that I can browse nested directories with shareable URLs.
11. As a repository visitor, I want to open files via `/blob/{ref}/{path}`, so that I can view individual files with shareable URLs.
12. As a repository visitor, I want directory listings sorted with **directories first**, then files, each group alphabetical, so that browsing matches common forge conventions.
13. As a repository visitor, I want clone instructions in a **collapsible** section on the home page, so that setup help remains available without dominating the code view.
14. As a repository visitor on an **empty** repository (no commits), I want an empty state with clone/push guidance and **no** file tree or README section, so that the UI reflects reality.

### Branches, tags, and default ref

15. As a repository visitor, I want a ref picker with **Branches** and **Tags** tabs, so that I can browse any branch head or tag.
16. As a repository visitor landing on the home page, I want the default ref to be **`main` if it exists, else `master`, else the first branch alphabetically**, so that the initial view is predictable.
17. As a repository visitor, I want URLs to encode the selected ref (`/tree/{ref}/...`, `/blob/{ref}/...`), so that links are bookmarkable for a specific branch or tag.
18. As a repository visitor switching refs, I want the same path preserved where possible, so that I can compare the same file across refs.

### File and blob viewing

19. As a repository visitor, I want text files displayed with **client-side syntax highlighting**, so that source code is readable.
20. As a repository visitor, I want a **1 MB maximum** for inline file display, so that oversized files do not overload the browser or API.
21. As a repository visitor viewing a file larger than the inline cap, I want a clear message and a download option, so that I can still retrieve the content.
22. As a repository visitor, I want **PNG, JPEG, GIF, and WebP** images previewed inline, so that common assets are visible in the browser.
23. As a repository visitor, I want **SVG files offered as download only** (not rendered inline), so that script injection via SVG is avoided.
24. As a repository visitor, I want other binary files to show “binary file not shown” with a download link, so that behavior matches familiar forge UX.
25. As a repository visitor, I want a **raw/download endpoint** for file bytes, so that I can download any blob regardless of inline preview rules.
26. As a repository visitor opening a `.md` file on a blob page, I want **rendered markdown by default** with a toggle to **raw source**, so that I can read docs comfortably or inspect exact content.

### README rendering

27. As a repository visitor, I want the README chosen from the repository root using GitHub-style precedence (`README.md`, `README.markdown`, `README`, `README.txt`, case-insensitive), so that the correct intro document is shown.
28. As a repository visitor, I want README markdown rendered with the same safe pipeline as blob markdown, so that formatting is consistent.
29. As a repository visitor on a repository with no qualifying README at the root, I want the README section omitted, so that the page is not cluttered.

### Markdown safety

30. As a security reviewer, I want markdown rendering to **disallow raw HTML in source** and **sanitize rendered output**, so that README and `.md` blob pages cannot execute scripts.
31. As a repository visitor, I want normal markdown formatting (headings, lists, links, code blocks) to work in README and markdown blobs, so that documentation remains useful.

### Replication, performance, and operations

32. As an operator, I want web content reads served from a **non-primary replica**, so that git operations on the primary are not competing with browser traffic.
33. As an operator, I want the API to select **any healthy non-primary replica** (falling back to the next if one is unavailable), so that read load spreads without hitting the primary.
34. As an operator, I want web reads to **never fall back to the primary** when replicas exist, so that the primary stays reserved for git protocol traffic.
35. As a repository visitor, I want a subtle **“syncing…” banner** when the serving replica’s replication watermark indicates it is behind the primary, so that I understand content may be briefly stale.
36. As a repository visitor, I want browsing to continue serving replica data while the banner is shown, so that the UI remains usable during catch-up.
37. As an operator, I want **eventual consistency** acceptable for web browsing (no read-your-writes via primary), so that the architecture stays simple in v1.
38. As an operator, I want **Redis-backed caching** of tree/blob/README responses with short TTL, so that repeated views do not hammer storage replicas.
39. As an operator, I want **Cache-Control headers** on public repository content responses, so that browsers and edge proxies can cache safely.
40. As an operator, I want **dedicated rate limits** for anonymous content browsing (stricter than authenticated API usage), so that public repos are not trivially scraped at storage cost.
41. As a developer running locally, I want the full browse experience to work against the default Docker Compose stack, so that I can develop and test without extra setup.

### API and web integration

42. As the frontend, I want REST endpoints for listing branches, listing tags, listing directory entries, fetching blob metadata/content, fetching README content, and downloading raw bytes, so that the SPA can render all browse surfaces.
43. As the frontend, I want OpenAPI-documented content endpoints, so that the generated TypeScript client stays in sync.
44. As a signed-in user, I want content endpoints to accept **session cookie auth only** in v1 (no PAT on browse endpoints yet), so that the web UI auth model is straightforward.
45. As a tester, I want integration tests covering public browse, private 404/403, empty repo, README precedence, inline cap, and replica routing, so that regressions are caught.

## Implementation Decisions

### Major modules

The work splits into six deep modules with narrow interfaces. Each encapsulates substantial behavior behind a surface that should change rarely.

#### 1. Repository Content Authorization (new, API-side)

**Interface:** given `{ ownerSlug, repoSlug, optionalUserId }`, return `Allowed | Denied(404|403) | RepositoryContext`.

**Responsibilities:**
- Resolve repository by owner slug + repo slug (user or organization owner).
- Public repository + anonymous or any caller → allow read.
- Private repository + anonymous → deny as **404**.
- Private repository + authenticated user without read role → deny as **403**.
- Private repository + owner, repo member with read+, or org member with read access → allow read.
- Reuse the same effective-role semantics as git `ReadGit` access checks, adapted for session identity (no PAT/SSH credential path in v1).

Does **not** perform git object reads; only authorization and repository metadata needed for routing.

#### 2. Web Read Replica Selector (new, API-side)

**Interface:** given `RepositoryId`, return `StorageReadTarget` for **web content only**.

**Responsibilities:**
- Load replication routing for the repository (existing replication control plane / routing query).
- Exclude the **primary** node from candidates.
- Select the first **healthy non-primary replica**; if unavailable, try the next healthy non-primary.
- Fail with a service-unavailable or not-found style error if no healthy non-primary replica exists — **do not** route web reads to primary.
- Expose replication lag signal (primary watermark vs replica applied watermark) for banner use.

Depends on HA storage replication metadata being available (watermarks, in-sync flags). Assumes parallel HA work delivers replica health and watermark reporting.

#### 3. Storage Content Service (extend storage nodes)

**Interface (internal HTTP, bearer-authenticated):** operations against a bare repo path on the node.

**Endpoints (conceptual):**
- `GET .../branches` — list local branch heads.
- `GET .../tags` — list tags.
- `GET .../tree?ref={ref}&path={path}` — directory listing for ref + path (root = empty path).
- `GET .../blob?ref={ref}&path={path}` — blob metadata + inline text content when under size cap; binary flag; size; content-type hint.
- `GET .../blob/raw?ref={ref}&path={path}` — raw bytes with appropriate `Content-Type` and `Content-Disposition`.
- `GET .../readme?ref={ref}` — resolved README blob per precedence rules (or 404 if none).

**Implementation approach:**
- Extend the existing storage internal HTTP server pattern (same bearer token auth as lifecycle APIs).
- Use git subprocess (`git ls-tree`, `git cat-file`, `git rev-parse`, etc.) against the bare repo on disk — no new git protocol reimplementation.
- Directory listing returns entries with name, path, type (tree/blob), and size where cheaply available.
- Sort order applied at API layer: directories first, then files, alphabetical within each group (storage may return unsorted; API normalizes).

#### 4. Repository Content API (new, API-side)

**Interface:** public HTTP routes under `/repository/...` or `/public/repository/...` (split by auth policy).

**Responsibilities:**
- On each request: authorize → select web read replica → call storage content service on that node → map response to DTOs.
- Attach replication lag metadata to responses when replica watermark trails primary (for UI banner).
- Apply **1 MB inline cap** at API or storage boundary; oversized blobs return metadata + `tooLarge: true` without inline body.
- Classify blobs for preview: text (highlight), image (png/jpeg/gif/webp), svg (download-only), binary (download-only).
- README endpoint applies root README precedence and returns markdown source for client rendering (or rendered HTML if server-side chosen later — v1 returns source + metadata; client renders).

**Caching:**
- **Redis** shared cache keyed by `{repositoryId}:{ref}:{path}:{endpointKind}` (and README separately).
- Short TTL (recommended 30–60 seconds) to limit staleness with replica reads.
- Public responses: `Cache-Control: public, max-age=...` aligned with Redis TTL.
- Private responses: `Cache-Control: no-store`.
- Invalidate implicitly via TTL only in v1 (no push-triggered invalidation).

**Rate limiting:**
- Dedicated anonymous rate limit policy for content endpoints (stricter than general API).
- Authenticated users use normal or elevated limits.

**Auth policy:**
- Public repo content routes: `AllowAnonymous`.
- Private repo content routes: require session JWT; authorization module enforces access.

#### 5. Repository Browse UI (extend Nuxt web app)

**Routes:**
- `/{owner}/{repo}` — home: root tree, README below, collapsible clone section, ref picker, optional syncing banner.
- `/{owner}/{repo}/tree/{ref}/{path?}` — directory view (path optional for ref root).
- `/{owner}/{repo}/blob/{ref}/{path}` — file view with preview modes.

**Components (conceptual):**
- Ref picker with **Branches | Tags** tabs.
- File tree / directory table (name, type, size).
- README markdown renderer (safe pipeline).
- Blob viewer: syntax-highlighted text (Shiki or equivalent), image preview, binary placeholder, download button.
- Markdown blob toggle: Rendered | Raw.
- Syncing banner driven by API lag flag.
- Empty repository state (reuse/integrate existing clone/push copy).

**Markdown rendering (client):**
- Restricted markdown (no raw HTML in source).
- Sanitize HTML after render.
- Shared component for README and markdown blob rendered mode.

**Default ref resolution (client or API):** `main` → `master` → first branch alphabetically; handle empty repo (no branches).

#### 6. Content Response Cache (new, API infrastructure)

**Interface:** `GetOrSet(key, factory, ttl)` backed by Redis.

**Responsibilities:**
- Serialize/deserialize content DTOs.
- Namespace keys per repository to avoid collisions.
- Graceful degradation if Redis unavailable (bypass cache, log warning) — assumption: Redis is available in production compose; document for local dev.

### API contracts (conceptual)

**Directory entry:**
```
{ name, path, type: "tree"|"blob", size?: number }
```

**Tree response:**
```
{ ref, path, entries: DirectoryEntry[], replicationLag?: { behind: boolean, message?: string } }
```

**Blob response:**
```
{ ref, path, size, isBinary, isTooLarge, contentType, encoding?, textContent?, previewKind: "text"|"image"|"svg"|"binary" }
```

**README response:**
```
{ ref, fileName, markdownSource, replicationLag?: ... }
```

**Refs response:**
```
{ branches: [{ name, commitSha }], tags: [{ name, commitSha }] }
```

**Raw download:** binary stream; `Content-Disposition: attachment` or `inline` based on preview rules.

### Authorization matrix

| Repository | Caller | Result |
|------------|--------|--------|
| Public | Anonymous | 200 |
| Public | Authenticated | 200 |
| Private | Anonymous | 404 |
| Private | Authenticated, no access | 403 |
| Private | Authenticated, read access | 200 |

### Default branch and empty repo

- Default ref for home/tree root: **`main` if exists, else `master`, else first branch alphabetically**.
- Empty repo (no branches / no commits): skip tree and README; show empty state + collapsible clone instructions.

### README precedence (repository root, case-insensitive)

1. `README.md`
2. `README.markdown`
3. `README`
4. `README.txt`

First match wins; if none, omit README section.

### Inline preview rules

| Kind | Inline behavior |
|------|-----------------|
| Text under 1 MB | Syntax-highlighted source |
| Markdown under 1 MB | Rendered default; raw toggle |
| PNG, JPEG, GIF, WebP under 1 MB | Image preview |
| SVG | Download only (no inline render) |
| Other binary | “Binary file not shown” + download |
| Any file over 1 MB | No inline body; download via raw endpoint |

### Dependencies on HA storage replication

- Web read replica selection requires per-repository replica set, primary id, replica health, and watermark / in-sync metadata from the replication control plane.
- Syncing banner uses explicit lag signal: replica `AppliedWatermark < PrimaryWatermark` (or equivalent `IsInSync == false` for the serving replica).
- If HA metadata is not yet available during initial implementation, **assume** it will be — stub lag as `behind: false` only as a temporary development fallback, not shipped behavior.

### Assumptions

- Session cookie JWT auth remains the web UI credential; PAT on content endpoints is **out of scope** for v1.
- Git smart HTTP and SSH continue to use existing read routing (primary or in-sync replica); **web browsing uses a separate routing policy** that excludes primary.
- Redis is added to the deployment stack (or an existing Redis instance is reused) for API content caching.
- OpenAPI sync regenerates the Nuxt TypeScript client after new endpoints land.
- No database schema changes required for browsing itself; optional future `DefaultBranch` column is **not** used — default ref is computed from git refs.

## Testing Decisions

### What makes a good test

Tests should assert **observable behavior** at module boundaries: HTTP status codes, response shapes, authorization outcomes, sorting order, README precedence, size-cap behavior, and routing to non-primary replicas. Avoid testing internal cache key formats or specific Redis serialization unless through behavior (cache hit reduces downstream calls).

### Modules to test

| Module | Test focus | Prior art |
|--------|------------|-----------|
| Repository Content Authorization | Public allow; private 404/403 matrix; org member read | `RepositoryAccessChecksControllerTests`, repository member query handler tests |
| Web Read Replica Selector | Never selects primary; falls back among non-primary; lag flag | HA replication routing tests, `RepositoryReplicationRoutingQuery` tests |
| Storage Content Service | `ls-tree` listing, blob read, README resolution, git error handling | `repo-storage-layer` integration scripts, storage HTTP server tests |
| Repository Content API | End-to-end auth + proxy + cap + cache headers | `RepositoryControllerTests`, `PublicDiscoveryControllerTests` patterns |
| Repository Browse UI | Route rendering, empty state, ref picker (component/e2e as feasible) | Existing Nuxt page tests if present; manual e2e script acceptable for v1 |

### Integration tests

- Public repo: list root tree, fetch README, open blob, raw download.
- Private repo: anonymous → 404; wrong user → 403; member → 200.
- Empty repo: empty state, no tree API 404 or dedicated empty flag.
- Oversized blob: no inline content; raw download works.
- SVG: no inline preview.
- Directory sort: directories before files.
- Web routing mock/stub: verify API never calls primary storage for content reads.

### Out of scope for automated tests in v1

- Visual regression of syntax highlighting themes.
- Redis cluster failover behavior.
- Load testing scrape scenarios.

## Out of Scope

- **Personal Access Token** authentication on content/browse endpoints (session only in v1).
- **Primary storage reads** for web UI (including read-your-writes after push).
- **Git history**: commit log, blame, compare, diffs.
- **Pull requests, issues, wikis**, and code search.
- **Web-based editing** or file upload through the UI.
- **Pagination** for large directories (v1 returns full listing).
- **LFS** object preview and download.
- **Submodules** special UI.
- **Archive download** (zip/tarball of ref).
- **Fork** and **star** features.
- **Server-side syntax highlighting** (client-only in v1).
- **SVG inline preview** even with sanitization (download only in v1).
- **Push-triggered cache invalidation** (rely on short TTL only).
- **Persisted default branch** in Postgres (computed from refs).
- **Wiki-style** README outside repository root.
- **Patent/legal** view, license detection, or dependency graphs.

## Further Notes

### Relationship to existing repo overview page

The current repository home page is clone/push instructions only. This PRD **replaces the home page layout** for repositories with commits: file tree first, README second, collapsible clone section. Clone/push guidance remains important for empty repos and onboarding.

### Relationship to HA storage replication PRD

Web browsing intentionally **diverges from git read routing**: git may read primary or in-sync replicas; the web UI reads **non-primary replicas only**. Lag is acceptable with a user-visible banner (replication watermark–driven). Coordinate with in-flight HA work so storage content APIs and watermark metadata are available on the same nodes serving bare repos.

### Relationship to git HTTPS PAT PRD

Git transport auth (PAT/SSH) and web session auth remain separate in v1. Permission rules should align, but credential types differ. Future work may add PAT-automated content API access for CI/tools.

### Suggested implementation order (tracer bullets)

1. Storage content service on one node (tree, blob, raw, branches, tags, readme) with bearer auth.
2. API authorization module + web replica selector + proxy to storage.
3. Public repo browse endpoints + Redis cache + rate limits.
4. Private repo auth + 404/403 behavior.
5. Nuxt routes and home layout (tree, README, ref picker).
6. Blob viewer (highlight, images, markdown toggle, download).
7. Syncing banner wired to replication lag metadata.
8. Integration tests and OpenAPI sync.

### Open questions deferred

- Exact Redis TTL and anonymous rate limit numbers (tune in implementation; start with 60s cache and conservative anonymous limits).
- Whether home page redirects to `/tree/{defaultRef}` or renders inline at `/` (both URLs may show the same layout; prefer single canonical component).
- Image `Content-Disposition: inline` vs attachment for previewable types (prefer inline for preview).
