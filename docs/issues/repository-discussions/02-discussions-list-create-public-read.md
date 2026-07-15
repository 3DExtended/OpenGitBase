<!-- forge: #187 -->

# Discussions list, create, and public read

## Metadata

- ID: disc-02
- Type: AFK
- Status: ready
- Source: docs/prd/repository-discussions.md

## Parent

[PRD: Repository Discussions (Threads, Code Comments, Notifications)](../../prd/repository-discussions.md)

## What to build

End-to-end **discussion foundation**: persistence, API, OpenAPI, and web UI for listing and creating discussions.

**Data model:** `discussions` with per-repository monotonic `number`, `title` (required), optional `body`, `status` (starts `Open`), `creatorUserId`, timestamps (`createdAt`, `updatedAt`). Internal surrogate key for joins.

**API:**
- List discussions for a repository; default sort `updatedAt` descending.
- Filter by status (tag and assignee filters deferred to later slices).
- Create discussion (authenticated, Reader+, not blocked); allocate next sequential number per repo.
- Get discussion by `/{owner}/{repo}/discussions/{number}`.

**Web UI:**
- **Discussions** nav entry on repository pages.
- List page at `/{owner}/{repo}/discussions`.
- Create form: required title, optional body; sign-in prompt for anonymous users on public repos.
- Detail page shows title, status badge, creator, timestamps; comment area placeholder until disc-04.

**Auth:** wire all endpoints through disc-01 authorization module.

## Acceptance criteria

- [ ] `discussions` table migrated; sequential `number` unique per `repositoryId`
- [ ] New discussion always starts in **Open** status
- [ ] List API returns discussions sorted by recently updated
- [ ] List filter by status works (Open, Engaged, Resolved, Dismissed)
- [ ] Create requires authenticated user with read access; anonymous rejected on public repo
- [ ] Public repo: anonymous can list and view discussion detail
- [ ] Private repo: 404/403 matrix matches disc-01
- [ ] URLs use `/{owner}/{repo}/discussions/{number}`
- [ ] Web UI: Discussions nav, list page, create flow, detail page (metadata only)
- [ ] OpenAPI documents list, create, and get endpoints
- [ ] API tests: create assigns incrementing numbers; list sort order; auth matrix smoke

## Blocked by

- [01-discussion-authorization.md](./01-discussion-authorization.md)

## User stories covered

- 1, 2, 3, 5, 7, 8, 12 (metadata), 14, 65, 66

## Notes

- Engaged, resolve, dismiss, assignee, and tags land in later slices; status enum may exist but only `Open` is set on create here.
- Register backend feature via `agentGenCli new backend-feature` per project conventions.
