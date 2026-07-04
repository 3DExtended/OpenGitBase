# Commit read API

## Metadata

- ID: cv-02
- Type: AFK
- Status: ready
- Source: docs/prd/commit-change-view.md

## Parent

[PRD: Commit Change View (Clickable Commits & Per-Commit Diff)](../../prd/commit-change-view.md)

## What to build

Repository-scoped read endpoint returning commit metadata, statistics, and either a unified diff or root file tree in a single JSON response.

**Endpoint:**

```
GET /api/repository/by-slug/{owner}/{slug}/commits/{sha}
```

**Response (conceptual):**

```
{
  sha, shortSha,
  message, authorName, authoredAt,
  parents: [{ sha, shortSha }],
  stats: { filesChanged, insertions, deletions } | null,
  kind: "diff" | "root",
  files: [...]
}
```

**Behavior:**

- Authorization identical to repository tree/blob read (public anonymous; private 404 for anonymous, 403 for authenticated outsiders).
- Replica selection, Redis caching, and replication lag metadata follow repository web browsing patterns.
- Private responses include `Cache-Control: no-store`.
- Prefix SHA resolution before diff work; response always includes canonical full `sha`.
- **404** for unknown repository, unknown commit, or ambiguous SHA prefix.
- Orchestrate storage client call from cv-01; map storage payload to OpenAPI-friendly DTOs.

## Acceptance criteria

- [ ] Authenticated member receives 200 with full metadata and diff for a linear commit on a test repo
- [ ] Root commit returns `kind: "root"` with file path entries, not hunks
- [ ] Response includes parent list with short and full SHAs
- [ ] Response includes diff stats for non-root commits
- [ ] Unique abbreviated SHA in path resolves; canonical full SHA returned in body
- [ ] Ambiguous prefix returns 404
- [ ] Public repo: anonymous GET returns 200
- [ ] Private repo: anonymous GET returns 404; outsider authenticated GET returns 403; member GET returns 200
- [ ] API integration tests cover linear diff, root tree, prefix resolve, ambiguous prefix, and auth matrix
- [ ] OpenAPI / generated client surface updated for web app consumption

## Blocked by

- [01-storage-commit-read-helpers.md](./01-storage-commit-read-helpers.md)

## User stories covered

- 1–8, 9–12, 35–36, 39, 40

## Notes

- No merge-request-scoped commit endpoint; this route is repository-global.
- Prior art: `RepositoryContentController` browse handlers, merge request commits list endpoint.
- Web API client method added in cv-04; DTO types can land here or with cv-04.
