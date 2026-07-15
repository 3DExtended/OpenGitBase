<!-- forge: #161 -->

# Merge request authorization

## Metadata

- ID: mr-01
- Type: AFK
- Status: ready
- Source: docs/prd/merge-requests.md

## Parent

[PRD: Merge Requests (Branch Protection, Review, and Server-Side Merge)](../../prd/merge-requests.md)

## What to build

Introduce a **merge request authorization** module on the API. All merge request endpoints call it before reading or mutating merge request data.

Rules (mirror repository content / git `ReadGit` membership semantics, session identity only — no PAT on web MR APIs in v1):

| Repository | Caller | Read merge requests | Create / comment / approve / merge (when not blocked) |
|------------|--------|---------------------|------------------------------------------------------|
| Public | Anonymous | Allow | Deny (401 / sign-in required) |
| Public | Authenticated, any | Allow | Allow if Reader+ for read actions; Writer+ for approve/merge; create requires push access to source branch |
| Private | Anonymous | **404 Not Found** | Deny |
| Private | Authenticated, no read access | **403 Forbidden** | Deny |
| Private | Owner, repo member (read+), or org member with read | Allow | Per role thresholds above |

Expose hooks for:

- `CanReadMergeRequests`
- `CanCreateMergeRequest(sourceRef)` — requires push access to source ref
- `CanParticipate` — comment; Reader+ and not blocked (align with discussion blocked-users when present)
- `CanApprove` — Writer+, not MR author
- `CanMerge` — per protected-branch merge permission threshold (default Writer+); stub allow Writer+ until mr-03 rules exist

Private merge request responses include `Cache-Control: no-store` where appropriate.

## Acceptance criteria

- [ ] Public repo: anonymous list and detail requests succeed
- [ ] Public repo: unauthenticated create/comment/approve returns sign-in required (401 or equivalent)
- [ ] Private repo: anonymous merge request requests return **404** (not 401)
- [ ] Private repo: signed-in user without membership returns **403**
- [ ] Private repo: owner and repo reader can read merge requests
- [ ] Create merge request denied when caller cannot push to source branch
- [ ] Approve denied for MR author and for Reader role
- [ ] Merge denied for Reader role (Writer+ allowed via stub until branch rules wired)
- [ ] Participation hook returns allow when blocked-users feature is not yet deployed
- [ ] Authorization module unit tests cover full public/private matrix
- [ ] API controller integration tests assert status codes for anonymous, outsider, member, and owner on list endpoint (stub controller acceptable until mr-06)

## Blocked by

- None — can start immediately

## User stories covered

- 3, 4, 5, 6, 85, 86, 87

## Notes

- Reuse shared read-access logic from discussion authorization / `RepositoryContentAuthorizationService` rather than duplicating org/repo member rules.
- Branch-level push checks for create can delegate to a stub returning Writer+ until mr-04 enforcement exists.
