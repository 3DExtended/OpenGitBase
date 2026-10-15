<!-- forge: #242 -->

# [slice] lfs-08 — Repository deletion cleanup and admin LFS figures

## Metadata

- Type: AFK
- Status: ready

## Parent

PRD discussion #234

## What to build

LFS disk space is reclaimed when a repository is deleted, and operators can see LFS replication health.

1. **Deletion.**
   - Deleting a repository deletes its `LfsObject` and `LfsObjectCopy` rows and calls a per-repository LFS `DELETE` (plaintext and encrypted roots) on every node that held a copy.
   - The `DELETE` is idempotent.
   - Nodes that are unreachable during deletion are cleaned up later: the reconciler removes LFS directories of repositories that no longer exist.
2. **Admin replication dashboard (API + web).**
   - Per repository: LFS object count, LFS bytes, copies missing per node (from the last inventory), and cold-recovery restore progress (from lfs-06).
   - Fleet totals: LFS bytes and number of missing copies.
3. **Fleet drift.** The fleet drift / storage inventory endpoints include LFS directories, so orphaned LFS data shows up there.

Demo: delete a repository with LFS objects — its LFS directories disappear on every node. The admin dashboard shows LFS counts and missing copies for another repository after one copy is removed by hand.

## Acceptance criteria

- [ ] Repository deletion removes LFS rows and LFS directories on all nodes
- [ ] A node that is offline during deletion is cleaned by the reconciler once it is back
- [ ] The per-repository LFS `DELETE` is idempotent (storage test)
- [ ] The admin dashboard shows LFS count, bytes, missing copies per node and restore progress
- [ ] Fleet drift includes orphaned LFS directories
- [ ] API controller tests and Playwright visual snapshots for the dashboard additions

## Blocked by

- #239

## User stories covered

- 45, 46
