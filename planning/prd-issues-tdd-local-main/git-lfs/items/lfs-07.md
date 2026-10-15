# lfs-07 — Web UI: LFS files in blob view and raw downloads

## Summary

- **Pointer detection (storage).**
  - `storage_lfs.parse_pointer` holds the one pointer parser: the exact git-lfs v1 form, at most 1024 bytes.
  - The pre-receive gate (lfs-04) now uses it as well.
  - Text that only looks like a pointer is an ordinary blob. That covers CRLF, a missing trailing newline,
    extension keys, an upper-case or short oid, a size with a leading zero, another spec version, and trailing or
    leading text.
- **Blob endpoint.** For a pointer, `get_blob` describes the LFS object instead of the pointer text:
  - `size` is the object's size, and `lfs` is `{oid, size}`;
  - `isBinary` is true, and `previewKind` comes from the path (image, svg or binary);
  - there is no `textContent` or `contentBase64`.

  The API adds `lfs.available`, which is true when the repository has an `LfsObject` row. Pointer blobs skip the
  content cache, because availability changes with uploads and repairs.
- **Raw endpoint.**
  - **Storage.** It still sends the pointer bytes but names the object in `X-OpenGitBase-Lfs-Oid` / `-Size`, so
    the API needs only one storage call.
  - **Streaming.** `RepositoryContentService` streams the object via `LfsContentReader`:
    - read replicas first, the write primary last, and a node listed in both roles is asked once;
    - unhealthy, failing or empty nodes are skipped;
    - nothing is buffered: the controller returns the storage response stream with `Content-Length` set to the
      recorded size.
  - **Content type.** The real type is used only for the images the blob view previews (png, jpg, gif, webp).
    Everything else, SVG and HTML included, is `application/octet-stream`, and every raw response carries
    `X-Content-Type-Options: nosniff`, so user content never runs in the API's origin.
  - **Missing objects:**
    - an object the repository never received answers `404` "LFS object not available.";
    - a recorded object that no node holds answers `503` with `Retry-After: 10` and queues a repair (lfs-05).
  - **Permissions.** Access goes through the existing content-read authorization first. An outsider of a private
    repository gets `403`, and neither storage nor LFS is touched.
- **Blob viewer (`RepoBlobViewer`):**
  - a "Stored with Git LFS" badge next to the real size;
  - an inline preview for LFS images up to 20 MiB, loaded from the raw endpoint;
  - every other LFS file, text included, is download only in v1;
  - a missing object shows "LFS object not available" with the `git lfs push --all origin` hint, and the
    download button is disabled.

  The decision lives in `utils/lfsBlob.ts` (`lfsBlobView`).
- **Repository usage.** The API client reads `gitBytes` and `lfsBytes` (lfs-02). `StorageUsageMeter` shows
  "Git: … · Git LFS: …" under the total when the repository has LFS bytes.
- **Visual gallery.** Three LFS blob states (image preview, download only, not available) and the storage meter
  with LFS.

## Linked Context

- PRD: `docs/prd/git-lfs.md` (forge #234)
- Work item: `lfs-07` (forge #241)

## Dependency Graph

### Direct dependencies (blocked by)

- lfs-01
- lfs-02

### Full chain

`lfs-01 -> lfs-02 -> lfs-07`

## Status

- Branch: `claude/lfs-implementation-difficulty-1a3fd2`
- Tests:
  - Storage, `python3 -m unittest discover` in `applications/repo-storage-layer`: 157 ran. The one error is the
    existing `test_storage_artifact_crypto` import (no `cryptography` module on the host). New tests:
    - `PointerTests` (4): an exact pointer, an empty object, look-alikes as subtests, and content over 1024 bytes;
    - `test_storage_content` (2): the blob of a pointer describes the LFS object, and a look-alike stays text;
    - `test_raw_blob_of_an_lfs_pointer_names_the_lfs_object_in_its_headers`.
  - `dotnet test tests/OpenGitBase.Api.Tests`: 871 passed. New tests:
    - `RepositoryContentControllerTests` (8):
      - blob, available and not available;
      - raw streams with type, length and nosniff;
      - html, svg and zip are served as octet-stream;
      - never uploaded answers 404;
      - recorded but on no node answers 503 + `Retry-After`;
      - a private outsider is refused without touching storage;
      - an ordinary file still returns its bytes.
    - `LfsContentReaderTests` (6): read replica first, fallback to the primary, failing and unhealthy nodes are
      skipped, a node in both roles is asked once, a repair is queued when no node holds the object, and the
      recorded size. A mutation check (primary first, no repair, no fallback) fails three of them.
  - Web: `pnpm vitest run` gives 29 files and 145 tests. New: `utils/lfsBlob.test.ts` (8).
  - Compose E2E against the rebuilt API and storage: `LfsWebContentTests` (new) passed. It pushes a 600 KB PNG
    with Git LFS to a private repository and checks:
    - the blob describes the object;
    - raw returns `image/png`, the exact length, `nosniff` and byte-identical content;
    - an outsider is refused.

    The lfs-07 code passed. The other `GitHttps.Lfs*` tests (lfs-01..06, 6/6 green at lfs-06) failed in this run
    on host overload (load average up to ~440), not on assertions about this change:
    - HAProxy marked both APIs down after 2 s health-check timeouts, so logins got its 503 page;
    - HAProxy's worker once aborted;
    - an async encrypted copy arrived after the test's 180 s deadline. The file was on storage-4 minutes later.

    They are run again in lfs-09.
- Visual snapshots:
  - `tests/visual/lfs-blob.spec.ts` is new, with 4 snapshots × 3 viewports.
  - The full-gallery baseline `visual-gallery-*` was regenerated for the four new sections.
  - Other gallery and page snapshots that fail locally do so because of relative-time drift ("x days ago") and are
    left as they are (separate task: freeze the clock in the visual tests).
- Commit(s): see `progress-log.md`
