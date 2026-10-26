<!-- forge: #238 -->

# [slice] lfs-04 — Pre-receive integrity gate for LFS pointers

## Metadata

- Type: AFK
- Status: ready

## Parent

PRD discussion #234

## What to build

A push must never publish a ref that points at an LFS object the server does not hold. The storage pre-receive hook detects the LFS pointers a push introduces, and the API rejects the push when any of their objects is not recorded.

1. **Pointer detection (storage, `storage_push_validation.py`).**
   - Covers blobs in the commits the push adds (the same commit range as today's push-rule validation).
   - A blob counts as a pointer when it is ≤ 1024 bytes and parses exactly as an LFS pointer: `version https://git-lfs.github.com/spec/v1`, `oid sha256:<64 hex>`, `size <n>`.
   - For each pointer, send `oid`, `size` and path to API push validation, next to the existing commit data.
   - Detection must stay cheap for large pushes: batch `cat-file`, and size-filter before reading content.
2. **Validation (API).**
   - Reject the push if any oid has no `LfsObject` row for the repository. Message: `LFS object <oid> (<path>) is missing on the server. Run: git lfs push --all origin <ref>`.
   - The API accepts push-validation payloads with and without the new field, so old and new hooks keep working during rollout.
3. **Scope.**
   - Pointers already in history before the push are not re-checked.
   - Web merges create no new pointer blobs and are unaffected.
   - The gate applies regardless of `Lfs:Enabled`: with LFS off, a push that introduces pointers to unrecorded objects is rejected the same way.

Demo: commit an LFS file, skip the upload with `GIT_LFS_SKIP_PUSH=1 git push` — the push is rejected with the missing oid and path. `git lfs push --all origin main`, then push again — accepted.

## Acceptance criteria

- [ ] Pointers are detected only in commits the push adds; small non-pointer blobs and malformed pointers are ignored
- [ ] A push with a pointer to an unrecorded object is rejected with oid, path and the fix command
- [ ] The same push is accepted after the objects are uploaded and verified
- [ ] Pushing a branch whose pointers were already pushed on another branch is accepted without re-upload
- [ ] The API accepts old hook payloads without the pointer field
- [ ] Python tests (`test_storage_push_validation.py`) and API push-validation tests cover the above
- [ ] Compose E2E covers the reject-then-accept flow

## Blocked by

- #235

## User stories covered

- 9
