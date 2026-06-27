# mr-01 handoff — Merge request authorization

## PRD

`docs/prd/merge-requests.md` — Authorization matrix, Implementation module hooks.

## Work item

`docs/issues/merge-requests/01-merge-request-authorization.md`

## Acceptance criteria (summary)

- Public: anonymous read OK; unauthenticated mutate → 401
- Private: anonymous → 404; outsider → 403; member read OK
- Create denied without source-branch push access
- Approve: Writer+ only, not author
- Merge: Writer+ stub until branch rules
- Blocked-user hook delegates to discussion block query
- Unit tests + stub controller integration tests for list auth matrix

## Dependencies

None.

## Branch

`main`

## Prior art

- `DiscussionAuthorizationService` + `DiscussionAuthorizationServiceTests`
- `RepositoryDiscussionsControllerTests`
