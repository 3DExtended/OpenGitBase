# lfs-03 — Encrypted quorum on verify

## Summary

- **Segmented AEAD (API).** `LfsObjectCipher` streams AES-256-GCM over fixed plaintext segments
  (`Lfs:SegmentBytes`, 1 MiB). The header holds `OLFS`, the format version, key version, segment size, plaintext
  size and a random 8-byte nonce prefix. Nonce = prefix plus the big-endian u32 segment index. AAD =
  `{repositoryId}:lfs:{oid}:{index}:{isFinal}`. Both directions hash the plaintext and compare it with the oid
  before the final segment is written. Decryption also checks the header, key version, trailing bytes and size.
- **Storage.** The encrypted store lives at `STORAGE_LFS_ENCRYPTED_ROOT` (`/var/lib/opengitbase/lfs`, outside
  the artifact tree) as `{repo}/aa/bb/{oid}.aead`. The 8081 routes are `HEAD/GET/PUT
  /internal/lfs/{repo}/encrypted/{oid}` (encrypted replica role only) and `/objects/{oid}` (refused on an
  encrypted replica). Both are streaming and need the node Bearer token.
- **Verify = LFS Quorum.**
  - `LfsReplicationService` resolves the repository's healthy nodes: the primary, plaintext replicas on other
    nodes, and the encrypted replicas.
  - It streams the object from the primary through `LfsEncryptingContent` (which sets Content-Length and
    buffers nothing) to every encrypted replica in parallel. Each upload is capped by
    `Lfs:ReplicaUploadTimeoutSeconds` (600), and verify returns at the first confirmed copy.
  - `LfsObject`, the primary's Plaintext copy and the confirmed Encrypted copy go into one transaction with
    `KeyVersion` = the Repository Key version.
  - Without quorum: `503` with `Retry-After` and no rows.
  - An already-recorded object returns `200` at once, without touching storage.
- **Async completion.** `LfsCopyCompletionScheduler` gives the remaining work its own DI scope. It waits for
  the other encrypted uploads, retries a failed one once, copies plaintext to the read replica (HEAD first)
  and records an `LfsObjectCopy` row for each copy that exists. The reconciler (lfs-05) repairs the rest.
- **No encrypted replica.** Upload batches that still have objects to upload answer `503` "LFS requires
  encrypted replication; this repository is still migrating. Try again later." Verify answers the same.
- **Read routing.** Download transfer targets are the healthy plaintext replicas first, then the primary. The
  dispatcher already falls back on `404`.
- **Retry-After.** `LfsResult.RetryAfterSeconds` (10 s) becomes the API's `Retry-After` header. The dispatcher
  relays it, so git-lfs retries verify later instead of failing at once.
- **The Repository Key never leaves the API.** Only ciphertext and plaintext object bytes go to storage nodes.

## Linked Context

- PRD: `docs/prd/git-lfs.md` (forge #234)
- Work item: `lfs-03` (forge #237)
- Handoff/design notes: `items/lfs-03-handoff.md`

## Dependency Graph

### Direct dependencies (blocked by)

- lfs-01

### Full chain

`lfs-01 -> lfs-03`

## Status

- Branch: `claude/lfs-implementation-difficulty-1a3fd2`
- Tests:
  - `dotnet test tests/OpenGitBase.Api.Tests` — 798 passed. New tests:
    - `LfsObjectCipherTests`: round trips at 0, 1, segment±1 and multi-segment sizes. Rejections: tamper,
      truncation, missing final segment, reorder, segment from another object, other repository, wrong key
      version, wrong key, oid mismatch before the final segment, short source.
    - `LfsReplicationServiceTests`: replica set; ciphertext decrypts and holds no plaintext; quorum with one
      hanging replica; give-up after the timeout; all failing; primary content not matching the oid; no key
      means no upload; the key never appears in any outgoing request (raw, hex, base64); async second
      encrypted copy and read-replica copy recorded; retry of a failed copy; down replica not recorded; no
      second plaintext copy when the read role sits on the primary.
    - `LfsCopyCompletionSchedulerTests`: completion in its own scope.
    - `LfsServiceTests`: 200 with quorum copies and KeyVersion; 503 with Retry-After and no row; migration 503
      for verify and upload batch; already-recorded verify; download targets read replica → primary.
    - `LfsInternalControllerTests`: `Retry-After` header.
    - `StorageLfsClientTests`: streaming GET and PUT, encrypted route.
  - `dotnet test tests/OpenGitBase.Features.Repository.Tests` — 74 passed (copy rows recorded with the object,
    `RecordLfsObjectCopyQueryHandler`)
  - `dotnet test tests/OpenGitBase.Dispatcher.Tests` — 57 passed (Retry-After relayed)
  - `python3 -m unittest test_storage_http_server test_storage_lfs` — 45 passed (role enforcement both
    ways, encrypted store outside the artifact tree, token)
  - `dotnet test tests/OpenGitBase.E2E.Tests --filter RequiresCompose!=true` — 29 passed
  - Compose E2E: `LfsEncryptedReplicaTests` checks that both encrypted replicas receive a `.aead` copy (one
    before verify answers, one afterwards) and that a recursive `grep` for the object's plaintext marker over
    `/var/lib/opengitbase` and `/srv` on each encrypted replica finds nothing. The `LfsObjectCopy` rows show
    storage-1 Plaintext plus storage-2 and storage-3 Encrypted, all with KeyVersion 1. `LfsPushCloneTests` and
    `LfsAccessAndQuotaTests` are still green (4/4).
- Visual snapshots: none (UI in lfs-07)
- Commit(s): see `progress-log.md`

### Notes

- Migration `AddLfsObjectCopies` adds the `LfsObjectCopy` table: PK (RepositoryId, Oid, StorageNodeId), an
  index on StorageNodeId, and a cascade to `LfsObject`.
- The `IStorageLfsClient` HttpClient now has no overall timeout. Replica copies bound themselves with
  `Lfs:ReplicaUploadTimeoutSeconds`. Verify's HEAD runs under the request's cancellation.
- In compose, the read role of a repository sits on its primary (storage-1), so the E2E has no second plaintext
  copy to wait for. The read-replica copy path is covered by `LfsReplicationServiceTests`.
- With `GIT_CURL_VERBOSE`, git-lfs copies every text-typed request body (detected from the content) to stderr.
  The E2E object therefore starts with binary bytes. A 3 MiB ASCII body filled the pipe and stalled the
  upload after about 32 KB.
- If recording fails after quorum (repository deleted mid-verify), copies already written stay on the nodes
  for lfs-08's cleanup.
