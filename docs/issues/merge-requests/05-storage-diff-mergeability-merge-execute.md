<!-- forge: #165 -->

# Storage diff, mergeability, and merge execute

## Metadata

- ID: mr-05
- Type: AFK
- Status: ready
- Source: docs/prd/merge-requests.md

## Parent

[PRD: Merge Requests (Branch Protection, Review, and Server-Side Merge)](../../prd/merge-requests.md)

## What to build

Extend the storage internal API with git compare and merge operations used by merge requests.

**Interfaces:**

- `GetDiff(baseSha, headSha)` — unified diff payload (files, hunks, line numbers for old/new sides)
- `CheckMergeability(targetSha, sourceSha)` — dry-run; returns mergeable / conflicts / unknown
- `ExecuteMerge(targetRef, sourceRef, strategy)` — merge commit, squash, or fast-forward; fail on conflict; no ref mutation on dry-run

**Behavior:**

- Fast-forward only when linear; return failure (not silent fallback) when FF impossible
- Merge writes route through existing primary / quorum write path
- Errors suitable for API propagation to merge dialog and mergeability banner

## Acceptance criteria

- [ ] Diff endpoint returns unified diff for two SHAs on a provisioned test repo
- [ ] Mergeability reports `mergeable` for linear, non-conflicting pair
- [ ] Mergeability reports `conflicts` when branches conflict
- [ ] Execute merge commit creates merge commit on target ref
- [ ] Execute squash produces single commit on target with expected message handling
- [ ] Execute FF updates target ref when linear; fails when not FF-able
- [ ] Execute merge fails without mutating refs when conflicts exist
- [ ] Storage unit/integration tests cover strategies and conflict case
- [ ] API layer can call storage endpoints via existing storage client pattern

## Blocked by

- [Git storage proxy](../git-storage-proxy/README.md) — storage nodes and internal HTTP API must be operational

## User stories covered

- 49, 50, 51, 52, 54, 56, 57, 58

## Notes

- Rebase merge strategy explicitly out of scope.
- Commits-since-merge-base listing for MR Commits tab can share storage git helpers.
