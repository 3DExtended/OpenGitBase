<!-- forge: #166 -->

# Merge request core (API + list, create, detail shell)

## Metadata

- ID: mr-06
- Type: AFK
- Status: ready
- Source: docs/prd/merge-requests.md

## Parent

[PRD: Merge Requests (Branch Protection, Review, and Server-Side Merge)](../../prd/merge-requests.md)

## What to build

End-to-end **merge request foundation**: persistence, API, OpenAPI, and web UI shell for listing, creating, and viewing merge requests.

Scaffold backend feature via `agentGenCli new backend-feature merge-request --withDatabase --withApi`.

**Data model:** `merge_requests` with per-repository monotonic `number`, `title`, optional `body`, `status`, `sourceRef`, `targetRef`, `sourceHeadSha`, `targetBaseSha`, `isDraft`, `creatorUserId`, timestamps.

**Status machine (v1 transitions in this slice):**

```
Create → Draft (isDraft) or Open
Draft → Open (publish)
Open → Closed
Draft → Closed
```

Approved / Merged transitions land in mr-07 and mr-08.

**Validation on create:**

- Creator can push to source ref (mr-01)
- Source ≠ target; source has commits ahead of target
- At most one active (Draft/Open/Approved) MR per `(sourceRef, targetRef)` pair

**API:**

- List merge requests; default sort `updatedAt` descending; filter by status
- Create, get by number, update title/body, publish, close
- Refresh source/target SHAs on read (or explicit refresh endpoint)

**Web UI:**

- **Merge requests** nav entry
- List page at `/{owner}/{repo}/merge-requests`
- Create form: source/target branch pickers (target defaults to default branch), title, body, draft checkbox, optional discussion link field (persisted in mr-15; UI field optional stub)
- Detail page shell: Overview tab with metadata and status badges; Commits/Changes/merge actions placeholder until later slices

Wire all endpoints through mr-01 authorization.

## Acceptance criteria

- [ ] `merge_requests` table migrated; sequential `number` unique per `repositoryId`
- [ ] Create as Draft or Open; publish transitions Draft → Open
- [ ] Duplicate active pair rejected with clear error
- [ ] Create rejected when source not ahead of target
- [ ] List sorted by recently updated; status filter works
- [ ] Close from Draft or Open
- [ ] SHAs stored and refreshed when branches move
- [ ] URLs use `/{owner}/{repo}/merge-requests/{number}`
- [ ] Public/private auth matrix matches mr-01
- [ ] Web UI: nav, list, create flow, detail shell with status badges
- [ ] OpenAPI documents list, create, get, publish, close
- [ ] Handler and API tests for numbering, dup guard, lifecycle

## Blocked by

- [01-merge-request-authorization.md](./01-merge-request-authorization.md)
- [02-default-branch-persistence-and-settings.md](./02-default-branch-persistence-and-settings.md)

## User stories covered

- 1, 2, 7, 8, 9, 10, 11, 13, 14, 16, 17, 18, 19, 22, 23, 88

## Notes

- Approved state exists in enum but transitions deferred to mr-07.
- Discussion link persistence deferred to mr-15; create form may collect numbers without backend until then.
