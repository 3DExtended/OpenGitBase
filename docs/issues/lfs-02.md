<!-- forge: #236 -->

# [slice] lfs-02 — Permissions, size limits, batch cap and quota

## Metadata

- Type: AFK
- Status: ready

## Parent

PRD discussion #234

## What to build

LFS follows the same access rules and storage limits as git, with clear errors the `git-lfs` client shows to the user.

1. **Permissions:** download needs read access and upload needs write access, with the same rules as `git-upload-pack` / `git-receive-pack`. Anyone without access to a private repository gets the same refusal as for a clone. Without write access an upload batch answers `403` while downloads keep working.
2. **Per-object cap:** `Lfs:MaxObjectBytes` (default 2 GiB). Larger objects get a per-object `422` error in the batch response, with the limit in the message.
3. **Batch cap:** `Lfs:MaxBatchObjects` (default 100). Larger batches answer `413`.
4. **Quota:** the sum of the sizes of objects not yet recorded, plus `StorageBytesUsed`, must stay within the repository's resolved byte limit (repository override, organization quota, platform default — the same resolution as git pushes). Otherwise the whole batch answers `507` with a message naming the limit.
5. **Usage accounting:** when verify records an object, its size is added to `StorageBytesUsed` once (logical size, not copy count), in the same transaction. Organization storage quota includes LFS bytes.
6. **Usage API:** the repository usage response reports `gitBytes` and `lfsBytes` separately (the UI follows in lfs-07).

Demo: an object above the cap, an upload that exceeds quota, and an upload by a read-only member each fail with a clear `git lfs push` message; a read-only member can still `git lfs pull`.

## Acceptance criteria

- [ ] Read-only member: upload batch `403`, download works
- [ ] Non-member on a private repository: batch refused like a clone (no information leak)
- [ ] Object above `Lfs:MaxObjectBytes`: per-object `422` with the limit in the message
- [ ] More than `Lfs:MaxBatchObjects` objects: `413`
- [ ] Quota exceeded: `507`; objects already recorded do not count twice
- [ ] Verify adds the size to `StorageBytesUsed` exactly once, even when retried or run concurrently
- [ ] Organization quota computation includes LFS bytes
- [ ] Usage API returns `gitBytes` and `lfsBytes`
- [ ] API tests cover each case; compose E2E covers the read-only and quota cases

## Blocked by

- #235

## User stories covered

- 7, 8, 11, 12, 34, 35, 36, 38 (and the API half of 37)
