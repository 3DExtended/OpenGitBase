# Server-side merge and discussion closes links

## Metadata

- ID: mr-08
- Type: AFK
- Status: ready
- Source: docs/prd/merge-requests.md

## Parent

[PRD: Merge Requests (Branch Protection, Review, and Server-Side Merge)](../../prd/merge-requests.md)

## What to build

**Server-side merge** orchestration and **discussion link** persistence with **`closes`** behavior on merge.

**Discussion links:**

- Table `merge_request_discussion_links` with `relationshipType`: `closes`, `related`, `implements`
- CRUD links on MR; optional parse `#n` from description on create/update
- On **Merged**, system resolves linked discussions for `closes` type (discussion **Resolved**)

**Merge orchestration:**

- `POST .../merge` with `strategy` (merge commit, squash, fast-forward) and `deleteSourceBranch` (default false)
- Verify status **Approved**, actor merge permission, mergeability clean at execution time
- Call mr-05 storage merge as platform identity (mr-04)
- Respect locked merge strategy on protected target rule
- On success: **Merged**, record merge commit SHA; optional delete source ref
- On conflict: fail without status change

**Mergeability API:**

- `GET .../mergeability` exposing checking / mergeable / conflicts / unknown

**Web UI:**

- Merge dialog with strategy picker and delete-source-branch checkbox
- Merge button disabled when not Approved or not mergeable
- Mergeability banner on detail page

## Acceptance criteria

- [ ] Merge rejected unless status is Approved
- [ ] Merge rejected when mergeability reports conflicts
- [ ] Successful merge commit lands on target; MR status Merged
- [ ] Squash and FF strategies work per mr-05
- [ ] Locked strategy on rule enforced
- [ ] Platform identity used for git write
- [ ] `closes` link resolves discussion on merge; `related`/`implements` unchanged
- [ ] deleteSourceBranch removes source ref when requested
- [ ] Race: merge fails safely if target moved into conflict between check and execute
- [ ] API and integration tests for merge happy path and conflict block

## Blocked by

- [04-git-push-enforcement.md](./04-git-push-enforcement.md)
- [05-storage-diff-mergeability-merge-execute.md](./05-storage-diff-mergeability-merge-execute.md)
- [07-approvals-and-merge-gates.md](./07-approvals-and-merge-gates.md)

## User stories covered

- 49, 50, 51, 52, 53, 54, 56, 57, 58, 12, 70, 71

## Notes

- **First demo milestone** completes with this slice.
- Requires discussions resolve handler from disc-03 for closes behavior.
