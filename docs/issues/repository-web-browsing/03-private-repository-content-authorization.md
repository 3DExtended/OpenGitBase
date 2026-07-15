<!-- forge: #204 -->

# Private repository content authorization

## Metadata

- ID: repo-browse-03
- Type: AFK
- Status: ready
- Source: docs/prd/repository-web-browsing.md

## Parent

[PRD: Repository Web Browsing (File Tree, Blob View, README)](../../prd/repository-web-browsing.md)

## What to build

Introduce a **repository content authorization** module on the API. All content endpoints (tree, blob, raw, readme, branches, tags) call it before proxying to storage.

Rules (mirror git `ReadGit` membership semantics, session identity only — no PAT in v1):

| Repository | Caller | Result |
|------------|--------|--------|
| Public | Anonymous or authenticated | Allow |
| Private | Anonymous | **404 Not Found** |
| Private | Authenticated, no read access | **403 Forbidden** |
| Private | Owner, repo member (read+), or org member with read | Allow |

Private responses include `Cache-Control: no-store`. Public responses do not set `no-store` (cache headers added in issue 09).

## Acceptance criteria

- [ ] Public repo: anonymous tree/blob/readme requests succeed
- [ ] Private repo: anonymous requests return **404** (not 401)
- [ ] Private repo: signed-in user without membership returns **403**
- [ ] Private repo: owner and repo reader can browse content
- [ ] Private repo: org member with read access can browse org-owned repo content
- [ ] Private content responses include `Cache-Control: no-store`
- [ ] Authorization module unit tests cover full public/private matrix
- [ ] API controller tests assert status codes for anonymous, outsider, member, and owner on tree and blob endpoints
- [ ] No regression: public browse from issue 02 still works

## Blocked by

- [02-public-root-tree-web-ui.md](./02-public-root-tree-web-ui.md)

## User stories covered

- 3, 4, 5, 6, 7, 44

## Notes

- Align role checks with `RepositoryAccessChecksController` read paths where possible; extract shared logic rather than duplicating org/repo member rules.
- Do not leak private repo existence via different error bodies for anonymous vs not-found owner.
