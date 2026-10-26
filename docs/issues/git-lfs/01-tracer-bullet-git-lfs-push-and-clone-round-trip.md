<!-- forge: #235 -->

# [slice] lfs-01 — Tracer bullet: git lfs push and clone round trip

## Metadata

- Type: AFK
- Status: ready

## Parent

PRD discussion #234

## What to build

The thinnest end-to-end path through all three tiers. The real `git-lfs` client pushes LFS objects over HTTPS and gets them back on clone, behind `Lfs:Enabled`.

**Not durable yet.** In this slice, verify records an object as soon as the write primary holds it. Encrypted quorum arrives in lfs-03. `Lfs:Enabled` defaults to `false` and must stay off in production until lfs-09.

1. **Storage node: plaintext object store.**
   - Content-addressed files `lfs/{repositoryId}/{oid[0:2]}/{oid[2:4]}/{oid}` on the persistent repos volume, **outside** the bare repository directory.
   - Streaming `PUT`: write to a temp file in the same directory, compute SHA-256 and byte count while reading, reject a mismatch, then atomic rename. A repeated put of an existing, correct object succeeds and changes nothing.
   - Streaming `GET`.
   - Exposed to the dispatcher on the git HTTP port (same trust model as `git-http-backend` there), and to the API on the internal HTTP port (`HEAD`/`GET`, Bearer node token).
   - Nothing is ever read whole into memory; no hex or JSON envelopes.
2. **API: `LfsObject` model and batch planning.**
   - New `LfsObject` entity and migration: `RepositoryId`, `Oid`, `Size`, `KeyVersion`, `CreatedAt`, `CreatedByUserId`; unique (`RepositoryId`, `Oid`).
   - Internal batch endpoint called by the dispatcher with the PAT, repository path, operation and objects.
   - *Download:* recorded objects get a download action; others get per-object `404`.
   - *Upload:* recorded objects get no actions; others get upload and verify actions.
   - Hrefs are built from the configured public git base URL (`Git:PublicBaseUrl`).
   - **LFS Action Token:** signed by the API; scoped to repository, oid, size and operation; carries `expires_in` (`Lfs:ActionTokenLifetimeSeconds`). It is validated only when a transfer starts.
   - Internal transfer-check endpoint: validates the action token and returns the routing targets and declared size.
   - Internal verify endpoint: `HEAD` on the primary confirms oid and size, then inserts the `LfsObject` row (idempotent; concurrent verifies of one oid are safe).
   - `Lfs:Enabled` switch: the batch answers `501` when off.
3. **Dispatcher: LFS routes.**
   - Extend path parsing for `POST .../info/lfs/objects/batch`, `PUT|GET .../info/lfs/objects/{oid}`, `POST .../info/lfs/objects/{oid}/verify` and `POST .../info/lfs/locks/verify`. Existing git paths must behave exactly as before.
   - Batch: Basic auth with the PAT, forwarded to the API; the response is relayed with `Content-Type: application/vnd.git-lfs+json`.
   - Upload: transfer check, raise the request body limit to exactly the declared size, then stream to the primary. **No** `EnableBuffering`.
   - Download: transfer check, then stream from the primary (read targets come in lfs-03), forwarding `Content-Length`.
   - Verify: forwarded to the API.
   - `locks/verify` stub returns `{"ours":[],"theirs":[]}`.
   - Only the `basic` transfer adapter and `sha256`; errors use the LFS body `{message, request_id}`.
4. **E2E:** install `git-lfs` in the E2E runner image; add a compose E2E test with `Lfs:Enabled=true`.

Demo: in local compose, `git lfs track "*.bin"`, commit a 5 MB file, push, clone into a new directory. The file is byte-identical, and a second push uploads nothing.

## Acceptance criteria

- [ ] `git lfs push` and `git clone` (with smudge) round-trip byte-identical content via local compose (compose E2E test)
- [ ] Re-pushing or pushing a new branch uploads no already-recorded object
- [ ] Upload with wrong content or size is rejected and nothing becomes visible under the oid; a retried upload succeeds
- [ ] Objects are stored outside the bare repository directory; `import-bundle` of the same repository leaves them intact (storage test)
- [ ] An action token for one oid, operation or repository is rejected for another; an expired token is rejected at transfer start
- [ ] `git push` prints no "locking API not supported" hint
- [ ] With `Lfs:Enabled=false` the batch answers `501`; existing git HTTP paths are unaffected (dispatcher tests)
- [ ] Upload streams without buffering; the body limit equals the declared object size (dispatcher tests)
- [ ] Controller, query-handler and dispatcher tests satisfy the coverage meta-tests

## Blocked by

None

## User stories covered

- 1, 2, 3, 4, 5, 6, 10, 13, 14, 22, 25, 47, 50
