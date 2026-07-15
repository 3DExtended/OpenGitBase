<!-- forge: #173 -->

# Post-push create banner

## Metadata

- ID: mr-13
- Type: AFK
- Status: ready
- Source: docs/prd/merge-requests.md

## Parent

[PRD: Merge Requests (Branch Protection, Review, and Server-Side Merge)](../../prd/merge-requests.md)

## What to build

After a user pushes a branch with commits **ahead of the default branch**, show a **Create merge request** banner on repository views — without auto-creating an MR.

**Behavior:**

- Detect when viewing repo context and current/default branch comparison shows N commits ahead (API helper or reuse content refs + compare metadata)
- Banner copy: branch name, commit count, link to create MR form pre-filled with source branch and default target
- Hide when active MR already exists for same source→target pair
- Hide on default branch itself
- Signed-in users with push access only (optional: show read-only hint for others)

**API (if needed):**

- Lightweight `GET .../branch-ahead-summary?ref=` returning `{ aheadCount, defaultRef, hasActiveMergeRequest }`

## Acceptance criteria

- [ ] Push feature branch → banner appears on repo home/tree for authenticated pusher
- [ ] Banner links to create MR with source prefilled
- [ ] No banner when duplicate active MR exists
- [ ] No banner when branch is default or not ahead
- [ ] No auto-create MR on push
- [ ] API test or UI test for ahead detection edge cases (empty repo, no default)

## Blocked by

- [02-default-branch-persistence-and-settings.md](./02-default-branch-persistence-and-settings.md)
- [06-merge-request-core-api-and-ui-shell.md](./06-merge-request-core-api-and-ui-shell.md)

## User stories covered

- 15, 16

## Notes

- Exact trigger (session branch vs last pushed ref) may use query param or recent push cookie — keep v1 simple: compare selected/current ref from browse context.
