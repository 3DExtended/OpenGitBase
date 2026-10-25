# lfs-05 — Reconciler repair from on-disk LFS inventory

## Summary

- **Inventory (storage).** `GET /internal/lfs/{repo}/inventory` on 8081 requires the node token. It answers
  `{"kind": "plaintext"|"encrypted", "objects": [{"oid", "size"}]}`, and the node's role for the repository
  decides the kind. `LfsObjectStore.inventory` lists only complete objects at their oid's place: partial uploads
  (dot files) and misplaced or foreign files are left out. `IStorageLfsClient.GetInventoryAsync` throws when a node
  cannot answer (status, body or kind), so an unreachable node is never taken for an empty one.
- **`LfsCopyReconciler`.** Each run handles one repository and trusts the disk, not `LfsObjectCopy`:
  - **Read.** Takes the inventory of every healthy node of the repository. Encrypted replicas are expected to
    hold encrypted copies; every other role holds plaintext. An unhealthy or unreachable node, or one that
    reports the wrong kind, is skipped, and its rows stay unchanged.
  - **Compare.** A plaintext copy counts as present when its size equals the recorded one. An encrypted copy
    counts when its length is plausible at some valid segment size (`LfsObjectCipher.IsPlausibleEncryptedLength`),
    which catches truncation and still accepts copies written before `Lfs:SegmentBytes` changed. A copy found
    gets `LastVerifiedAt`, and a row if it had none. A copy not found loses its row.
  - **Log.** Every missing copy is logged as `LFS object <oid> of <repository> missing on <node>`: a warning when
    a row existed, information when the copy was never recorded there.
  - **Repair order.** Plaintext copies first, so a plaintext copy restored from an encrypted replica can feed
    the encrypted repairs of the same run. Within each kind the longest-missing copy goes first (last verified,
    else stored, else object creation). At most `Lfs:MaxRepairsPerRun` (100) repairs per run.
  - **Repair sources:**
    - plaintext: another plaintext node, else decrypt from an encrypted replica;
    - encrypted: re-encrypt from a plaintext node.

    Every result is logged: `Repaired LFS object … on <node> from <source>`, or `Could not repair … : <reason>`.
- **Decrypted restores are verified.** `LfsDecryptingContent` streams `LfsObjectCipher.DecryptAsync`, which
  authenticates each segment and checks the oid before writing the final one. A bad copy never becomes a complete
  upload, and storage hashes plaintext against the oid again before the rename. The transfer primitives
  (`CopyPlaintextAsync`, `CopyEncryptedAsync`, the new `RestorePlaintextAsync`) are now public on
  `LfsReplicationService`. They take a cancellation token and are shared with the verify path.
- **`LfsRepairQueue`.** A hosted worker that runs one repository at a time, each in its own DI scope, so large
  copies never hold up the HA loop's failover checks.
  - Requests for a repository already waiting collapse into one.
  - A run that hit the cap and repaired something queues the repository again.
  - A run that repaired nothing waits for the next request.
- **Reconciler step "LFS copies".** Runs after "degraded latch" in `AntiEntropyReconcilerService.RunOnceAsync`
  and queues every repository that has LFS objects. The reconciler runs at API start and every 900 s.
- **New replicas.** A replica added by rebalance or backfill has an empty inventory, so the same path fills it
  with no separate copy job.
- **Recorded but unavailable.** When no download target holds the object, the dispatcher answers `503` with
  `Retry-After: 10` (instead of `404`) and calls `POST /api/v1/internal/lfs/repair` with the download's action
  token. The API checks the token (download, same repository) and queues a repair. git-lfs 3.7 honours
  `Retry-After`, so the clone succeeds once the object is back.

## Linked Context

- PRD: `docs/prd/git-lfs.md` (forge #234)
- Work item: `lfs-05` (forge #239)

## Dependency Graph

### Direct dependencies (blocked by)

- lfs-03

### Full chain

`lfs-01 -> lfs-03 -> lfs-05`

## Status

- Branch: `claude/lfs-implementation-difficulty-1a3fd2`
- Tests:
  - `python3 -m unittest test_storage_http_server test_storage_lfs test_storage_push_validation` — 63 passed.
    Inventory: plaintext and encrypted by role, empty repository, partial and misplaced files skipped, invalid
    repository id, token required.
  - `dotnet test tests/OpenGitBase.Api.Tests` — 843 passed. New tests:
    - `LfsCopyReconcilerTests` (14):
      - encrypted copy lost on disk while recorded, re-encrypted;
      - plaintext copied from the other plaintext node (no decrypt);
      - plaintext decrypted from an encrypted replica when none is left;
      - tampered encrypted copies never written as plaintext;
      - rows of missing copies deleted, `LastVerifiedAt` of found ones set;
      - unreachable node skipped with rows unchanged while others are repaired;
      - unhealthy node not asked;
      - new replica filled with every object;
      - unrecorded copies on disk get a row;
      - wrong-size copy treated as missing;
      - longest-missing first with the cap;
      - repair log line;
      - wrong-kind inventory skipped;
      - repository without objects asks no node.

      A mutation check (accepting any encrypted length) makes the wrong-size test fail.
    - `LfsRepairQueueTests` (5): own scope per run, collapse while waiting, re-queue after a capped run, no
      re-queue without progress, a failing run does not stop the worker.
    - `AntiEntropyReconcilerServiceTests`: the step queues each repository with LFS objects, and none without.
    - `LfsServiceTests`: a repair request with a download token queues the repository; an upload token gets 401
      and a foreign-repository token gets 403, and neither queues anything.
    - `StorageLfsClientTests`: inventory parsing, kind, failures throw.
    - `LfsObjectCipherTests`: plausible lengths at any valid segment size; truncated, padded and impossible
      lengths rejected.
  - `dotnet test tests/OpenGitBase.Dispatcher.Tests` — 58 passed. When no target holds the object: 503 +
    Retry-After + repair request, and also when the repair request itself fails.
  - `dotnet test tests/OpenGitBase.Features.Repository.Tests` — 74 passed
  - `dotnet test tests/OpenGitBase.E2E.Tests --filter RequiresCompose!=true` — 29 passed
  - Compose E2E `LfsReconcilerRepairTests` (52 s):
    1. An encrypted replica's LFS directory of the repository is deleted while the DB lists the copy.
    2. Stopping and starting `api-1` runs the reconciler; the `.aead` copy comes back.
    3. Every plaintext copy is deleted.
    4. A clone sees `503`, retries and gets the object with the right oid; the plaintext copies are back on disk.

    The API logs show the expected paths: re-encrypted from storage-1 on api-1, then decrypted from storage-3
    on api-2 after the dispatcher's repair request. The LFS E2E set (`GitHttps.Lfs*`) passes 6/6.
- Visual snapshots: none
- Commit(s): see `progress-log.md`

### Notes

- On its first pass the reconciler also gave encrypted copies to objects that older E2E runs on the stack
  recorded before lfs-03 ("never recorded there").
- Each API instance runs its own repair worker. Both may repair the same copy at once; each writes a valid copy,
  and the copy rows tolerate the race (a failed save only skips that bookkeeping until the next run).
- The repair worker uses the repository's current key. Key rotation for LFS copies is out of scope.
- `LfsStorageDisk` now holds the E2E helpers for replica roles, files on disk, `docker exec` and waiting for
  health, shared by the encrypted-replica and repair tests.
