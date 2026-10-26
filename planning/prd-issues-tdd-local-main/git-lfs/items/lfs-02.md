# lfs-02 — Permissions, size limits, batch cap and quota

## Summary

- **Permissions:** upload batches use the git write rules, download batches the read rules (shared
  `RepositoryAccessCheckEvaluator`). A refused upload answers 403 when the caller can read the repository
  (repository reader, organization member, read-scoped PAT) and 404 otherwise, exactly like a clone of a
  private repository; download keeps working for readers.
- **Caps:** `Lfs:MaxObjectBytes` (2 GiB) → per-object 422 naming the limit; `Lfs:MaxBatchObjects` (100) → 413.
- **Quota:** for uploads, the distinct not-yet-recorded objects (within the object cap) plus `StorageBytesUsed`
  must fit the repository limit resolved like git pushes (override → organization quota → platform default;
  `RepositoryAccessCheckEvaluator.ResolveRepositoryBytesLimitAsync` is now shared). Otherwise 507 with
  needed/left/limit bytes. Gated by `RepositoryStorageQuota:Enabled` like git.
- **Accounting:** `RecordLfsObjectQueryHandler` inserts the `LfsObject` row and adds its size to
  `StorageBytesUsed` (atomic `ExecuteUpdate`) in one transaction inside the execution strategy; a concurrent
  verify that loses the primary-key race rolls back and adds nothing. Since git pushes are checked against
  `StorageBytesUsed` too, LFS bytes count against the same (organization) quota.
- **Usage API:** `GET /repository/{id}/usage` adds `gitBytes` and `lfsBytes` (sum of `LfsObject.Size`);
  `bytesUsed` = live git bytes (when storage reports them) + LFS bytes.

## Linked Context

- PRD: `docs/prd/git-lfs.md` (forge #234)
- Work item: `lfs-02` (forge #236)

## Dependency Graph

### Direct dependencies (blocked by)

- lfs-01

### Full chain

`lfs-01 -> lfs-02`

## Status

- Branch: `claude/lfs-implementation-difficulty-1a3fd2`
- Tests:
  - `dotnet test tests/OpenGitBase.Api.Tests` — 750 passed (new: reader/org-member 403, private-repo
    non-member indistinguishable from missing repo, object cap 422, batch cap 413, quota 507 incl. organization
    quota and dedupe/recorded exclusion, quota disabled, download over quota; usage split in the controller)
  - `dotnet test tests/OpenGitBase.Features.Repository.Tests` — 70 passed (usage added once on insert, on
    retry, and when a concurrent verify wins the insert — staged deterministically with a transaction
    interceptor; usage handler reports `gitBytes`/`lfsBytes`)
  - `dotnet test tests/OpenGitBase.Common.Tests` — 89, `Features.Organization.Tests` — 55,
    `E2E.Tests` (no stack) — 27 passed
  - Compose E2E: `LfsAccessAndQuotaTests` — read-only member clones byte-identical and `git lfs push` is
    refused with "write access" (no PUT); upload past the quota fails with "quota exceeded" (no PUT).
    `LfsPushCloneTests` still green.
- Visual snapshots: none (UI in lfs-07)
- Commit(s): see `progress-log.md`

### Notes

- The quota E2E sets `StorageBytesUsed` through `docker exec … psql`: per-repository overrides require an
  organization-run fleet of more than three nodes, which compose does not have.
- A read-only member's plain `git push` already stops at receive-pack discovery (403) before the git-lfs
  pre-push hook runs, so the E2E drives the batch API with `git lfs push`.
- Usage baselines of `RepositoryMemberAuthMatrixTests` (not asserted) were patched to the new response shape.
- Observation: git pushes never update `StorageBytesUsed` today (usage shows live disk bytes from storage);
  the git quota check therefore effectively sees LFS bytes only. Out of scope here.
