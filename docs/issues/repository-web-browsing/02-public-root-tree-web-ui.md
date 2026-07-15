<!-- forge: #203 -->

# Public root tree in the web UI

## Metadata

- ID: repo-browse-02
- Type: AFK
- Status: ready
- Source: docs/prd/repository-web-browsing.md

## Parent

[PRD: Repository Web Browsing (File Tree, Blob View, README)](../../prd/repository-web-browsing.md)

## What to build

First end-to-end tracer bullet: **public repositories only**.

Add API content endpoints that resolve a repository by owner slug + slug, verify it is **public**, proxy to a storage node content API (issue 01), and return directory listing DTOs. For local development before web-replica routing (issue 08), proxy to any healthy storage node holding the repository copy.

Compute **default ref**: `main` if exists, else `master`, else first branch alphabetically.

Update the Nuxt repository home page (`/{owner}/{repo}`) to show a **root directory table** for the default ref: name, type, size. Sort entries **directories first**, then files, alphabetical within each group. Link directories toward future tree routes (stub or `#` until issue 04).

OpenAPI-document new public content routes; sync generated TypeScript client.

## Acceptance criteria

- [ ] Public repo: anonymous `GET` tree at root returns 200 with sorted directory entries
- [ ] Private repo: content endpoints return 401 or 404 at this stage (full matrix in issue 03)
- [ ] Default ref selection: `main` → `master` → first alphabetical branch
- [ ] Directory sort: folders before files, alphabetical within groups
- [ ] Repository home renders root file table for a public repo with commits
- [ ] API controller tests cover public tree success, default ref logic, and sort order
- [ ] Query handler or service unit tests cover storage proxy mapping and entry sorting
- [ ] Automated test (API integration or component test) verifies home page receives and displays entries for a seeded public repo

## Blocked by

- [01-storage-content-http-api.md](./01-storage-content-http-api.md)

## User stories covered

- 1, 8, 12, 16, 42, 43

## Notes

- Replica-only routing deferred to issue 08; do not block this slice on HA completion.
- Reuse `GetRepositoryByOwnerSlugQuery` for metadata resolution.
