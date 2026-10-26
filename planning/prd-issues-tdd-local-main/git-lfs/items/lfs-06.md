# lfs-06 — Cold recovery and post-promotion LFS restore

## Summary

- **Cold recovery (`ColdRecoveryService`).** Once the bare repository is imported and verified, the repository is
  `Rf4Healthy` and accepts pushes again, and the LFS restore is queued (`ILfsRepairQueue`, the lfs-05 path). The
  restore runs in the background:
  - it decrypts every object from an encrypted replica in the API;
  - the cipher authenticates each segment and checks the oid before the final segment is written;
  - storage hashes the plaintext against the oid again before the rename.

  Meanwhile:
  - the pointer gate (lfs-04) checks `LfsObject` rows, not disk, so pushes go through;
  - downloads of objects not restored yet answer `503` + `Retry-After` and ask for a repair.

  Before scheduling the restore, recovery:
  - **Adds a Primary replica row for the target** when the node had none. The target is the first healthy
    plaintext node and can be new to the repository, as storage-4 is in compose. Routing and the reconciler find
    the primary through that row; without it the repository had no routable primary after a cold recovery onto
    a fresh node.
  - **Forgets the plaintext copy rows of the lost nodes.** The rows then show the restore's progress, and a node
    that comes back with its copies gets them recorded again by the next reconcile. The target's own rows are
    kept, so running the recovery again does not throw away what was already restored. A failed recovery changes
    no copy rows and queues nothing.
- **Hot promotion (`PromotePrimaryReplicaQueryHandler`).** Both the RF4 promotion of an in-sync read replica and
  the RF3 promotion queue an immediate LFS repair. Objects the promoted node never received are restored ahead of
  the next reconciler pass. A healthy primary, or a lagging read replica with no recovery, queues nothing.
- **Corrupt encrypted copies (`LfsCopyReconciler`):**
  - **Detection.** `LfsReplicationService.RestorePlaintextAsync` reports when the source failed decryption
    (`LfsCopyAttempt.SourceFailedVerification`, with an `LfsCipherException` anywhere in the chain HttpClient
    wraps). The reconciler then tries the other encrypted replica.
  - **Forgetting the copy.** The damaged copy loses its row, which is saved even when no repair succeeded. It is
    logged as `Encrypted copy of LFS object … on <node> failed verification; it is re-encrypted from plaintext`.
  - **Re-encryption.** The copy is queued in the same run and re-encrypted from the plaintext that was just
    restored.
  - **Source order.** Nodes are read in node-id order, so sources are tried in the same order every run.
- **Primary without a replica row.** The reconciler adds the repository's `PrimaryStorageNodeId` as a plaintext
  node when no replica row names it.
- **Restore progress.** `GetLfsCopyStatusQuery` (Features.Repository) returns `ObjectCount`, `TotalBytes` and, per
  node of the repository, kind, primary flag, `Copies` and `Missing`. It reads the copy rows that every repair
  writes. lfs-08 shows it on the admin dashboard.
- **Interrupted restores** resume through the reconciler step "LFS copies" (lfs-05). It queues every repository
  with LFS objects at API start and every 900 s. Each run restores only what is missing on disk, so runs are
  idempotent.

## Linked Context

- PRD: `docs/prd/git-lfs.md` (forge #234)
- Work item: `lfs-06` (forge #240)

## Dependency Graph

### Direct dependencies (blocked by)

- lfs-05

### Full chain

`lfs-01 -> lfs-03 -> lfs-05 -> lfs-06`

## Status

- Branch: `claude/lfs-implementation-difficulty-1a3fd2`
- Tests:
  - `dotnet test tests/OpenGitBase.Api.Tests` — 855 passed. New tests:
    - `ColdRecoveryServiceTests` (5):
      - writable and restore queued once git is back;
      - Primary row added for a node new to the repository, the old primary becomes Replica;
      - lost plaintext rows forgotten, encrypted rows kept;
      - a repeated recovery keeps one Primary row and the copies restored so far;
      - a failed import queues nothing and keeps the rows.

      A mutation check (no schedule, no row, no forget, forget the target's rows too) fails four of them.
    - `PromotePrimaryReplicaQueryHandlerTests` (4 new): RF4 and RF3 promotions schedule a repair; a healthy
      primary and a lagging read replica without recovery do not.
    - `LfsCopyReconcilerTests` (3 new, 1 changed):
      - one corrupt encrypted replica: the object is restored from the other, and the corrupt copy is
        re-encrypted;
      - a primary without a replica row is filled;
      - after cold recovery every object is decrypted onto the new primary, and no lost node is asked;
      - both encrypted copies tampered: nothing is written, and both rows go (0 repaired, 4 failed).
  - `dotnet test tests/OpenGitBase.Features.Repository.Tests` — 79 passed. New: `GetLfsCopyStatusQueryHandlerTests`
    (4): counts per node and kind, primary first, rowless primary, unknown repository.
  - `dotnet test tests/OpenGitBase.Dispatcher.Tests` — 58 passed
  - `dotnet test tests/OpenGitBase.E2E.Tests --filter RequiresCompose!=true` — 29 passed
  - Compose E2E `GitHttps.Lfs*` against the rebuilt API — 6/6 passed (3 m 55 s)
- Visual snapshots: none
- Commit(s): see `progress-log.md`

### Notes

- The cold-recovery chaos E2E (stop and wipe the node with primary + read replica, clone byte-identical) is part of
  lfs-09. In compose, storage-1 holds both roles for every repository, so stopping it cold-recovers every
  repository on the stack at once.
- `PromotePrimaryReplicaResult.NewPrimaryStorageNodeId` after a cold recovery still reports the old primary: the
  handler reads it from its own context, and the recovery saved the change in another. Nothing reads the field
  today.
