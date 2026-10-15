<!-- forge: #241 -->

# [slice] lfs-07 — Web UI: LFS files in blob view and raw downloads

## Metadata

- Type: AFK
- Status: ready

## Parent

PRD discussion #234

## What to build

LFS-tracked files look and behave like real files in the web UI instead of showing pointer text.

1. **Pointer detection.** The storage blob content endpoint recognizes pointer blobs (exact pointer format, ≤ 1024 bytes) and returns `lfs: { oid, size }` with the blob. The API adds `available` (whether an `LfsObject` row exists for the repository).
2. **Blob viewer.**
   - "Stored with Git LFS" badge, the real size, and a download button.
   - Image types are previewed inline.
   - No inline rendering of LFS text files in v1.
   - A missing object shows "LFS object not available".
3. **Raw endpoint.** For pointer blobs it streams the object content from a read target through the API, with the correct `Content-Type` and `Content-Length`, under the existing content-read authorization (private repositories need membership).
4. **Repository usage.** Git bytes and LFS bytes are shown separately (API from lfs-02).
5. **Visual snapshots.** Gallery fixtures for the LFS states (image preview, binary download, not available) with Playwright snapshots.

Demo: browse to an LFS-tracked PNG — it renders inline with the badge and its size. The raw link downloads the real file. A pointer to a missing object shows "not available".

## Acceptance criteria

- [ ] Pointer blobs are recognized exactly per the spec; files that merely look like pointers are not
- [ ] Blob view shows badge, size and download; images preview inline; missing objects show "not available"
- [ ] Raw endpoint returns the real content with correct `Content-Type` and `Content-Length`, streamed (not buffered)
- [ ] Private repository: raw LFS content refused for non-members, like other content reads
- [ ] Repository usage shows git and LFS bytes separately
- [ ] Storage content tests, API controller tests, Vitest for pointer handling, and Playwright visual snapshots for each LFS state

## Blocked by

- #235
- #236

## User stories covered

- 37, 39, 40, 41, 42, 43, 44
