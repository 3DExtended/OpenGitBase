# lfs-08 — Repository deletion cleanup and admin LFS figures

## Summary

- **Per-repository LFS delete (storage).**
  - `LfsObjectStore.delete_repository` removes one repository's LFS directory. `repositories()` lists the
    repositories that have LFS data under a root.
  - `DELETE /internal/lfs/{repositoryId}` (node token) deletes from the plaintext and the encrypted root and
    answers `{"deleted": true|false}`. A second call answers `deleted: false`, so the delete is idempotent.
  - A malformed id answers 404. Other repositories are untouched.
  - The on-disk inventory (`GET /internal/repos/inventory`) adds `lfsRepositories`: every repository with LFS data
    under either root.
- **Repository deletion (API).**
  - Before the row goes, `DeleteRepositoryWithStorageQueryHandler` collects every node that may hold LFS data:
    replica nodes, the storage node, the primary, and every node with an `LfsObjectCopy` row.
  - The database cascade removes `LfsObject` and `LfsObjectCopy` with the repository.
  - After the row is gone, the handler calls `IStorageLfsClient.DeleteRepositoryAsync` on each node (2 min
    timeout). An unhealthy node, a missing token or a failing delete only logs a warning; the deletion still
    succeeds.
  - When the repository delete misses its quorum, LFS rows and data stay untouched, so a repository that still
    exists never loses objects.
- **Offline nodes.** The new reconciler step "LFS orphans" (`LfsOrphanCleaner`, after "LFS copies") cleans up
  nodes that missed the delete.
  - It asks every healthy node for its `lfsRepositories`, then deletes the LFS data of each repository that has
    no row any more. A node that was down during the deletion is cleaned on the first pass after it is back.
  - It asks the node first and the database second, so a repository created in between counts as existing.
  - A database with no repository at all (fresh or wrong) deletes nothing and logs a warning.
  - A node whose inventory cannot be read, or a failing delete, only costs its own cleanup.
- **Fleet drift.** `FleetDriftKind.OrphanLfs`: a node reports LFS data for a repository that no longer exists.
  The storage inventory client reads `lfsRepositories` into `StorageInventoryResult.LfsRepositories`.
- **Admin API.**
  - `GET admin/repositories/{id}/replication` adds `lfs`:
    - object count and bytes, and `missingCopies` over all nodes;
    - per node: kind (plaintext or encrypted), `isPrimary`, `copies`, `missing`.

    It comes from `GetLfsCopyStatusQuery` (lfs-06), so it uses the same expected copies as the reconciler. That
    rule now lives in `LfsCopyExpectation`.
  - `GET admin/lfs/summary` returns the fleet totals: objects, bytes, missing copies, and repositories with
    missing copies (`GetLfsFleetTotalsQuery`).
- **Admin web.**
  - The repository detail page shows `AdminLfsCopyStatus` when the repository has LFS objects: totals, the
    missing-copies badge, one progress bar per node, and "Restoring LFS objects to {node}: {restored} of
    {total}" while the primary misses objects (cold recovery, lfs-06).
  - The repository list shows `AdminLfsFleetSummary` and refreshes it with the existing poll.
  - The logic lives in `utils/adminLfs.ts` (`lfsCopyPercent`, `lfsRestoreProgress`).

## Linked Context

- PRD: `docs/prd/git-lfs.md` (forge #234)
- Work item: `lfs-08` (forge #242)

## Dependency Graph

### Direct dependencies (blocked by)

- lfs-05

### Full chain

`lfs-01 -> lfs-03 -> lfs-05 -> lfs-08`

## Status

- Branch: `claude/lfs-implementation-difficulty-1a3fd2`
- Tests:
  - Storage, `python3 -m unittest discover` in `applications/repo-storage-layer`: 167 ran. The one error is the
    existing `test_storage_artifact_crypto` import (no `cryptography` module on the host). New tests:
    - `test_storage_lfs` (5): delete removes one repository and keeps the others; deleting a repository without
      objects changes nothing; an invalid id is refused; `repositories()` with data and with a missing root.
    - `LfsInternalEndpointTests` (5):
      - delete removes the plaintext and encrypted data;
      - delete is idempotent;
      - delete requires the node token;
      - a malformed id answers 404;
      - the inventory names every repository with LFS data.
  - `dotnet test tests/OpenGitBase.Api.Tests`: 887 passed. New tests:
    - `DeleteRepositoryWithStorageQueryHandlerTests` (4):
      - rows and data go on every node;
      - a failing LFS delete still deletes the repository and cleans the other nodes;
      - a failed quorum keeps LFS rows and data;
      - a node with only copy rows is cleaned too.
    - `LfsOrphanCleanerTests` (5):
      - orphans go and existing repositories stay;
      - a node that was down is cleaned once it is back;
      - an unreadable inventory or a failing delete does not stop the other nodes;
      - an empty database deletes nothing.
    - `AntiEntropyReconcilerServiceTests`: the "LFS orphans" step runs even when an earlier step fails.
    - `GetFleetDriftReportQueryHandlerTests`: `OrphanLfs`.
    - `StorageProvisionerClientTests` (2): `lfsRepositories` is read, and a node without it has none.
    - `AdminRepositoryReplicationControllerTests` (3): LFS figures with the copies each node misses, a repository
      without LFS, and `GetLfsSummary`.
  - Mutation check:
    - dropping the LFS delete from the repository deletion fails 3 tests;
    - deleting every candidate, and removing the empty-database guard, each fail one `LfsOrphanCleaner` test.
  - `dotnet test tests/OpenGitBase.Features.Repository.Tests`: 82 passed. New: `GetLfsFleetTotalsQueryHandlerTests`
    (2).
  - Also run: `OpenGitBase.Features.StorageNode.Tests` (42), `OpenGitBase.Dispatcher.Tests` (58), and the E2E
    meta tests (`RequiresCompose!=true`, 29).
  - Web: `pnpm vitest run` gives 30 files and 152 tests. New: `utils/adminLfs.test.ts` (7).
  - Compose E2E: `LfsRepositoryDeletionTests` (new). It pushes an LFS object, waits for every replica's copy,
    deletes the repository, and checks that no storage node keeps its plaintext or encrypted LFS directory. It
    could not run during this slice: the Docker daemon stopped answering under host load. It passed later, with the
    full `GitHttps.Lfs*` suite in lfs-09 (see `lfs-09.md`).
- Visual snapshots:
  - `tests/visual/admin-lfs.spec.ts` is new, with 2 snapshots × 3 viewports: copies with a restore in progress,
    and fleet totals.
  - The full-gallery baseline `visual-gallery-*` was regenerated for the two new sections.
  - A run without `--update-snapshots` passed (9/9).
- Commit(s): see `progress-log.md`
