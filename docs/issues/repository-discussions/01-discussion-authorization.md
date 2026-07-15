<!-- forge: #186 -->

# Discussion authorization

## Metadata

- ID: disc-01
- Type: AFK
- Status: ready
- Source: docs/prd/repository-discussions.md

## Parent

[PRD: Repository Discussions (Threads, Code Comments, Notifications)](../../prd/repository-discussions.md)

## What to build

Introduce a **discussion authorization** module on the API. All discussion endpoints call it before reading or mutating discussion data.

Rules (mirror repository content / git `ReadGit` membership semantics, session identity only — no PAT in v1):

| Repository | Caller | Read discussions | Create / comment (when not blocked) |
|------------|--------|------------------|-------------------------------------|
| Public | Anonymous | Allow | Deny (401 / sign-in required) |
| Public | Authenticated, any | Allow | Allow if Reader+ and not blocked |
| Private | Anonymous | **404 Not Found** | Deny |
| Private | Authenticated, no read access | **403 Forbidden** | Deny |
| Private | Owner, repo member (read+), or org member with read | Allow | Allow if not blocked |

Expose a participation gate hook (`IsParticipationAllowed`) that delegates to the blocked-users store when present; return allow when no block list exists yet so later slices can plug in without changing callers.

Private discussion responses include `Cache-Control: no-store` where appropriate.

## Acceptance criteria

- [ ] Public repo: anonymous list and detail requests succeed
- [ ] Public repo: unauthenticated create/comment returns sign-in required (401 or equivalent)
- [ ] Private repo: anonymous discussion requests return **404** (not 401)
- [ ] Private repo: signed-in user without membership returns **403**
- [ ] Private repo: owner and repo reader can read discussions
- [ ] Private repo: org member with read access can read org-owned repo discussions
- [ ] Participation hook returns allow when blocked-users feature is not yet deployed
- [ ] Authorization module unit tests cover full public/private matrix
- [ ] API controller integration tests assert status codes for anonymous, outsider, member, and owner on list endpoint (stub controller acceptable until disc-02)

## Blocked by

- None — can start immediately

## User stories covered

- 4
- 63

## Notes

- Align role checks with `RepositoryContentAuthorizationService` and `RepositoryAccessChecksController` read paths; extract shared logic rather than duplicating org/repo member rules.
- Do not leak private repo existence via different error bodies for anonymous vs outsider where browse uses the same pattern.
