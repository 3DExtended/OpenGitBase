# lfs-04 — Pre-receive integrity gate for LFS pointers

## Summary

- **Pointer detection (storage, `storage_push_validation.py`).** For each ref update,
  `git rev-list --objects <new> --not --all` lists the objects the push adds. In pre-receive the refs have not
  moved yet, so `--all` is the history before the push. Then:
  - one `cat-file --batch-check` keeps blobs of at most 1024 bytes;
  - one `cat-file --batch` reads only those candidates;
  - a blob counts as a pointer when it matches the exact v1 form (`version …`, `oid sha256:<64 hex>`,
    `size <n>`, trailing newline).

  Each oid is reported once, with the first path and ref it was found at. The hook sends `lfsPointers`
  (`oid`, `size`, `path`, `refName`) next to the existing commit data.
- **Scope.** Pointers already in history are not listed again: a branch that reuses an existing pointer sends
  nothing. A new branch covers all of its commits that no existing ref contains, not just the tip.
- **Validation (API).** `RepositoryInternalPushValidationController` checks the pointers once the push rules
  have allowed the push. It rejects when any oid has no `LfsObject` row, with this message:
  `LFS object <oid> (<path>) is missing on the server. Run: git lfs push --all origin <branch>`. When more
  objects are missing it says "(and N more)".
  - The gate runs whether or not `Lfs:Enabled` is set.
  - `LfsPointers` defaults to empty, so payloads from older hooks validate as before.
- **E2E harness.** `GitOperations` now drains stdout and stderr together. Reading them one after the other could
  deadlock once git or git-lfs filled the stderr pipe; that was the root cause of the lfs-03 E2E stall.
  `LfsGit.PatRemoteAsync` replaces three copies of the PAT-remote helper.

## Linked Context

- PRD: `docs/prd/git-lfs.md` (forge #234)
- Work item: `lfs-04` (forge #238)

## Dependency Graph

### Direct dependencies (blocked by)

- lfs-01

### Full chain

`lfs-01 -> lfs-04`

## Status

- Branch: `claude/lfs-implementation-difficulty-1a3fd2`
- Tests:
  - `python3 -m unittest test_storage_push_validation` — 9 passed. The tests use real bare repositories, with
    new commits written without refs as in pre-receive quarantine. They cover:
    - a pointer with its path and ref;
    - history pointers ignored;
    - a new branch: only commits not on other branches;
    - small non-pointer, malformed, trailing-garbage and >1024-byte blobs ignored;
    - each oid reported once;
    - deleted refs;
    - the payload carries `lfsPointers`;
    - the API reason is surfaced.
  - Storage suites together (`http_server`, `lfs`, `push_validation`) — 54 passed.
  - `RepositoryInternalPushValidationControllerTests` — 5 passed: missing object rejected with oid, path and
    fix; several missing named with a count; all recorded allowed; an old payload without the field still
    validates.
  - `dotnet test tests/OpenGitBase.Api.Tests` — 802 passed
  - `dotnet test tests/OpenGitBase.E2E.Tests --filter RequiresCompose!=true` — 29 passed
  - Compose E2E: `LfsPointerGateTests` covers three pushes:
    - With `GIT_LFS_SKIP_PUSH=1` the push is rejected, and stderr names the oid, `assets/big.bin` and
      `git lfs push --all origin main`.
    - After `git lfs push --all` the same push is accepted.
    - A `feature` branch that reuses the pointer is accepted without uploading again.

    The LFS E2E set (`GitHttps.Lfs*`) passes 5/5 with the new harness.
- Visual snapshots: none
- Commit(s): see `progress-log.md`
