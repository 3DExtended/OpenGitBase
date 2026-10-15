<!-- forge: #234 -->

# PRD: Git LFS

## Problem Statement

Teams that keep large binary files in their repositories — game and design assets, datasets, ML models, media, vendored binaries — cannot use OpenGitBase well today.

- **Git LFS does not work at all.** A `git lfs push` against OpenGitBase fails: the dispatcher answers `404` for every `/info/lfs/...` path. Teams that already use LFS on GitHub or GitLab cannot migrate, and repositories imported with LFS pointer files show the pointer text in the web UI with no way to get the real content.
- **Plain git punishes large binaries.** Every version of every binary lives in the pack forever. Clones get slow and the repository storage quota (1 GiB by default) fills up. Where a repository sets a `MaxFileSize` push rule, such files are blocked entirely, with no alternative.
- **Replication cost grows with total history size.** Encrypted replication builds a full `git bundle --all` for every push. A repository with large binaries re-bundles and re-encrypts all of them on each push. Large bundles have already caused failures: the 30 MB relay limit (#413) and the ~83 MB JSON string limit fixed since.

LFS was declared a non-goal in the git storage proxy, HA storage replication, git HTTPS PAT and repository web browsing PRDs. This PRD lifts that non-goal.

The constraint that makes this more than a protocol exercise: OpenGitBase promises that an acknowledged write survives loss of the primary+read node, and that encrypted replica nodes never see plaintext. LFS objects must get the same guarantees. Otherwise they would be the only repository content that can be silently lost on failover.

---

## Solution

Developers use the standard `git-lfs` client against OpenGitBase over HTTPS with the same personal access token they use for git. No extra configuration is needed: `git lfs track`, `git push`, `git clone`, `git lfs pull` and `git lfs fetch` work as they do on other forges.

LFS objects live on the existing storage fleet, next to the repository they belong to, and follow the four-copy model:

- Plaintext copies on the **write primary** and the **read replica**, which serve downloads.
- Encrypted copies on both **encrypted replicas**, encrypted with the repository's existing Repository Key. Encrypted replica nodes never see plaintext.

An upload is acknowledged only after the object is stored on the primary **and** on at least one encrypted replica (the same quorum as a git push). A push whose commits reference LFS objects the server does not durably hold is rejected, with a message that tells the user how to fix it. So a ref never points at an LFS object that only exists on one node.

Because LFS objects are immutable and addressed by their SHA-256, they need no watermarks, epochs or ordering. Replication means "copy what is missing", and any node holding a plaintext copy can serve a download.

LFS bytes count against the repository's storage quota, and each object has a size cap. In the web UI, LFS files show a "Stored with Git LFS" badge and their size; images are previewed; download and raw links return the real content.

The anti-entropy reconciler checks the LFS objects actually on each node's disk (not the database's belief) and repairs missing copies. Cold recovery restores LFS objects from encrypted replicas.

```
git-lfs client
     │  POST /{owner}/{repo}.git/info/lfs/objects/batch   (PAT, Basic auth)
     ▼
HAProxy ──► Dispatcher ──► API: authorize, quota, plan actions, mint action tokens
                │
                │  PUT/GET /{owner}/{repo}.git/info/lfs/objects/{oid}   (action token)
                ├── upload ──────► Write Primary   (plaintext, SHA-256 checked while streaming)
                └── download ◄──── Read Replica, fallback Write Primary
                │
                │  POST .../objects/{oid}/verify
                ▼
               API: stream from primary ─► encrypt (segmented AES-256-GCM) ─► Encrypted Replica A ┐ quorum:
                                                                         └─► Encrypted Replica B ┘ primary + ≥1 encrypted
                    then async: plaintext copy to read replica, second encrypted copy
```

---

## User Stories

### Using LFS from git

1. As a developer, I want `git lfs push` to work against OpenGitBase over HTTPS, so that I can version large binaries the way I do on other forges.
2. As a developer, I want LFS to authenticate with the same personal access token git already uses through my credential helper, so that I do not configure a second credential.
3. As a developer, I want `git clone` and `git lfs pull` to download LFS content automatically, so that my working tree contains real files, not pointers.
4. As a developer, I want LFS endpoints discovered automatically from my `.git` remote URL, so that I do not set `lfs.url` by hand.
5. As a developer, I want objects the server already holds skipped during upload, so that re-pushing or pushing a new branch does not re-upload unchanged binaries.
6. As a developer, I want an upload whose bytes do not match the announced SHA-256 or size rejected, so that a corrupted transfer never becomes repository content.
7. As a developer, I want a clear error when an object exceeds the maximum LFS object size, so that I know why the upload failed and what the limit is.
8. As a developer, I want a clear error when my upload would exceed the repository storage quota, so that I can clean up or ask for more space.
9. As a developer, I want a push rejected when it references LFS objects the server does not hold, with the missing object and path named and the command to fix it (`git lfs push --all origin`), so that I never publish a branch others cannot check out.
10. As a developer, I want a failed or interrupted upload to be safe to retry, so that flaky connections do not leave half-written objects behind.
11. As a developer without write access, I want LFS uploads refused with `403` while downloads still work, so that LFS follows the same permissions as git.
12. As a developer cloning a private repository I am not a member of, I want LFS downloads refused like the clone itself, so that LFS does not leak private content.
13. As a developer, I want `git push` not to print a "locking API not supported" hint on every push, so that LFS feels native even though locking is not offered yet.
14. As a developer migrating from GitHub or GitLab, I want to push a repository with existing LFS history (`git lfs push --all`), so that my full history and its binaries move over.
15. As a developer using an SSH remote, I want documented guidance to point `lfs.url` at the HTTPS endpoint, so that LFS works while SSH LFS is not supported.

### Durability and replication

16. As a repository owner, I want an LFS upload acknowledged only after the primary and at least one encrypted replica hold the object, so that acknowledged LFS content survives loss of the primary+read node.
17. As a repository owner, I want LFS objects copied to the read replica and the second encrypted replica after quorum, so that all four copies exist without slowing uploads down.
18. As a repository owner, I want downloads served from the read replica and from the primary when the read replica lacks an object, so that LFS reads stay available while replication catches up.
19. As a security reviewer, I want LFS objects stored on encrypted replicas only as ciphertext under the Repository Key, so that compromising an encrypted replica node reveals no LFS content.
20. As a security reviewer, I want the encryption to bind each segment to its repository, object and position, so that ciphertext cannot be truncated, reordered or swapped between objects or repositories undetected.
21. As a security reviewer, I want the Repository Key used for LFS never sent to a storage node, so that LFS does not widen key distribution.
22. As the system, I want the same object announced twice or uploaded concurrently to end up stored once and recorded once, so that retries and parallel pushes are harmless.
23. As the system, I want an object to count as present only once it has reached quorum, so that the batch API never tells a client to skip an upload that is not durable yet.
24. As the system, I want LFS uploads refused with a clear message for repositories that have no encrypted replicas yet, so that no LFS object is acknowledged without the quorum guarantee.
25. As the system, I want LFS objects stored outside the bare repository directory, so that cold recovery, re-cloning and rebalance, which replace that directory, cannot wipe them.

### Recovery and repair

26. As an operator, I want the reconciler to compare the LFS objects on each node's disk with the objects recorded for the repository, so that copies lost on disk are found even when the database still says healthy.
27. As an operator, I want a missing encrypted copy re-created from a plaintext copy, so that encrypted durability recovers without manual steps.
28. As an operator, I want a missing plaintext copy restored from the other plaintext copy, or decrypted from an encrypted replica when no plaintext copy exists, so that downloads recover after node loss.
29. As an operator, I want a newly assigned replica (rebalance, RF4 backfill) filled with the repository's LFS objects, so that replacing a node restores full LFS coverage.
30. As an operator, I want cold recovery to restore a repository's LFS objects from its encrypted replicas and verify each against its SHA-256, so that losing both plaintext copies loses no LFS content.
31. As a developer, I want a download of an object that is recorded but not yet restored to answer `503` with `Retry-After`, not `404`, so that my client retries instead of reporting the object as gone.
32. As an operator, I want each repository and each node in the LFS repair step to fail on its own, so that one unreachable node does not block repairs elsewhere.
33. As an operator, I want every LFS repair and every missing copy logged with repository, object and node, so that I can audit what the reconciler did.

### Quota and limits

34. As a platform operator, I want LFS bytes counted against the repository storage quota (once per object, regardless of copy count), so that LFS cannot bypass storage limits.
35. As a platform operator, I want a configurable maximum LFS object size, so that a single object cannot exhaust node disks or exceed replication timeouts.
36. As a platform operator, I want a configurable maximum number of objects per batch request, so that a single request cannot tie up the API.
37. As a repository owner, I want repository usage in the UI to show LFS bytes separately from git bytes, so that I understand where my quota goes.
38. As an organization owner, I want LFS bytes to count toward organization storage limits the same way git bytes do, so that contributed-capacity credits apply to LFS too.

### Web UI

39. As a web user, I want an LFS-tracked file shown with a "Stored with Git LFS" badge and its real size instead of pointer text, so that I see what the file is.
40. As a web user, I want to download an LFS file from the blob view and get the real content, so that I can use the file without a git client.
41. As a web user, I want LFS images previewed inline, so that browsing an asset repository is useful.
42. As a web user, I want the raw link of an LFS file to return the real content with the right content type and length, so that links and embeds work.
43. As a web user, I want an LFS file whose object is missing on the server shown as "LFS object not available", so that a broken reference is visible instead of a confusing pointer.
44. As a web user on a private repository, I want LFS downloads to follow the same authorization as other content reads, so that links do not leak private files.

### Operations and administration

45. As an operator, I want LFS object count, bytes and missing copies per repository in the admin replication dashboard, so that LFS replication health is visible.
46. As an operator, I want deleting a repository to delete its LFS objects on every node, so that disk space is reclaimed.
47. As an operator, I want an `Lfs:Enabled` switch, so that I can roll out all components before any client can upload.
48. As an operator, I want the rollout order documented (API, then storage nodes primary last, then dispatchers, then enable), so that mixed versions never accept an upload they cannot replicate.
49. As an operator, I want the storage sizing impact documented (each object stored as plaintext on the primary/read node(s) and twice encrypted), so that I can provision disks.

### Local development and testing

50. As a developer of OpenGitBase, I want the compose stack and E2E runner to include the `git-lfs` client, so that LFS is tested end to end with the real client.
51. As a developer of OpenGitBase, I want chaos tests that upload LFS objects, lose the primary+read node, cold-recover, and verify the downloaded bytes, so that the durability guarantee stays protected by tests.

---

## Implementation Decisions

### Terms

- **LFS Object** — an immutable blob identified by `oid` (lowercase hex SHA-256 of its content) and `size`, belonging to one repository.
- **LFS Pointer** — the small git blob (≤ 1024 bytes) committed in place of the file: `version https://git-lfs.github.com/spec/v1`, `oid sha256:<64 hex>`, `size <n>`.
- **LFS Quorum** — the object is stored on the write primary and on at least one encrypted replica, and is recorded in the control plane.
- **LFS Action Token** — a short-lived credential minted by the API for one object transfer (repository, oid, size, operation).

### Placement: storage fleet, not the Layer Store

LFS objects are stored on the storage nodes that already hold the repository, in the four-copy model from the Encrypted Replica Storage PRD (#13). The MinIO-backed Layer Store is not used. It exists for CI base images and dependency layers, is not part of the production compose, runs as a single instance, and storing LFS there would leave LFS without encrypted replicas, cold recovery or the reconciler.

### Protocol surface (dispatcher)

The dispatcher implements the Git LFS HTTP API (batch API + `basic` transfer adapter) under the repository's git path. HAProxy needs no change, because `/{owner}/{repo}.git/...` already routes to the dispatchers.

| Method | Path | Purpose |
|---|---|---|
| `POST` | `/{owner}/{repo}.git/info/lfs/objects/batch` | Batch API (upload/download planning) |
| `PUT` | `/{owner}/{repo}.git/info/lfs/objects/{oid}` | Upload one object |
| `GET` | `/{owner}/{repo}.git/info/lfs/objects/{oid}` | Download one object |
| `POST` | `/{owner}/{repo}.git/info/lfs/objects/{oid}/verify` | Verify: make the upload durable |
| `POST` | `/{owner}/{repo}.git/info/lfs/locks/verify` | Stub: always `{"ours":[],"theirs":[]}` |

- **Content type** `application/vnd.git-lfs+json`. Errors use the LFS error body `{ "message", "request_id" }` and the spec's status codes. Batch: `401` (with `WWW-Authenticate`), `403`, `404`, `413` (too many objects), `422` (invalid request), `507` (quota). Per-object errors go inside the batch response: `404` not found, `422` invalid oid or object too large.
- **Transfer adapter:** `basic` only; `hash_algo` `sha256` only. Other requested adapters are ignored and the response says `"transfer": "basic"`.
- **Batch authentication** is the same as git over HTTPS: Basic auth with a personal access token. Download requires read access and upload requires write access, with the same rules as `git-upload-pack` / `git-receive-pack`. Branch protection does not apply to objects; the batch request's `ref` field is ignored in v1.
- **The API answers the batch.** The dispatcher forwards the batch request with the token and repository path to a new internal API endpoint and relays the response. The dispatcher holds no LFS logic beyond parsing paths, enforcing body limits and streaming bytes.
- **Action hrefs** point back at the dispatcher, built from the same configured public git base URL that clone URLs use. Every action carries `header.Authorization: Bearer <LFS Action Token>` and `expires_in`. Every upload action includes a `verify` action.
- **Transfers:** for `PUT`/`GET`/`verify`, the dispatcher forwards the action token to an API transfer check. The check returns the routing targets (as the access check does today) and the object's declared size. The token is validated only when a transfer starts, so a long upload is not cut off when the token expires mid-stream.
- **Upload:** the dispatcher raises the request body limit to exactly the declared object size for this request and streams the body to the write primary without buffering it. `EnableBuffering` is not used, unlike pushes.
- **Download:** the dispatcher streams from the first read target that holds the object (read replica, then primary), forwarding `Content-Length`. If the object is recorded but no target has it, the dispatcher answers `503` with `Retry-After` and the API schedules a restore for that repository.
- **SSH:** not supported in v1 (`git-lfs-authenticate` and `git-lfs-transfer` are rejected). SSH users point `lfs.url` at the HTTPS endpoint.

### Storage nodes

- **Plaintext object store** (write primary and read replica): content-addressed files `lfs/{repositoryId}/{oid[0:2]}/{oid[2:4]}/{oid}` on the persistent repos volume, **outside** the bare repository directory. Cold recovery's bundle import deletes and re-creates that directory, and rebalance re-creates repositories from git, so objects inside it would be lost. A node that holds both the primary and read roles of a repository stores one plaintext copy.
- **Encrypted object store** (encrypted replica): `lfs/{repositoryId}/{oid[0:2]}/{oid[2:4]}/{oid}.aead` under a root on the persistent data volume that is separate from the per-watermark artifact tree, so watermark scans and artifact pruning never touch it.
- **Writes are streamed and checked:** the node writes to a temporary file in the same directory, computes SHA-256 and byte count while reading, rejects a mismatch, and moves the file into place with an atomic rename. Writing an object that already exists with the right content succeeds and changes nothing.
- **Role enforcement:** plaintext endpoints refuse to work on a node that holds only the encrypted role for the repository, and encrypted endpoints refuse to work on the primary or the read replica.
- **Endpoints:**
  - Dispatcher-facing object `PUT`/`GET` on the git HTTP port, with the same trust model as `git-http-backend` there today (the sec-04 hardening covers both). nginx forwards LFS locations to the storage HTTP server; `GET` may be served by nginx directly from disk.
  - API-facing object `PUT`/`GET`/`HEAD` (plaintext or encrypted by role), per-repository `DELETE`, and a per-repository LFS inventory (oid + size, plaintext or encrypted) on the internal HTTP port with the node's Bearer token.
- **No whole-object buffering anywhere:** no reading an object into memory and no hex or JSON envelopes. These caused the 30 MB body limit failure (#413) and the 83 MB bundle failure.

### Encryption format

- **The API encrypts and decrypts.** For LFS the Repository Key never leaves the API. When encrypted copies are created, the API streams plaintext from a plaintext node, encrypts it, and streams ciphertext to the encrypted replicas. When copies are restored, it streams ciphertext, decrypts it, and streams plaintext to the target node. This differs from git bundles, where the primary encrypts. Doing it in the API means no key handoff to storage nodes, one implementation of the crypto in C#, and the API already decrypts bundles during cold recovery.
- **Segmented AEAD:** a single AES-GCM operation needs the whole object in memory and is limited to ~64 GiB per nonce, so objects are encrypted in fixed plaintext segments (default 1 MiB):
  - Each segment uses AES-256-GCM with the Repository Key and a nonce built from a random 64-bit per-object prefix and a 32-bit segment counter.
  - Each segment's associated data is `{repositoryId}:lfs:{oid}:{segmentIndex}:{isFinal}`. This rejects reordering, truncation, and swapping segments between objects or repositories.
  - A header records the format version, key version, segment size and plaintext size.
- **Decryption checks the content:** after decrypting, the API verifies the SHA-256 of the restored plaintext against the oid before it writes the plaintext anywhere.

### Control plane (API)

**Schema (conceptual):**

```
LfsObject {
  RepositoryId: Guid
  Oid: string            // 64 lowercase hex; unique per (RepositoryId, Oid)
  Size: long
  KeyVersion: int        // Repository Key version used for its encrypted copies
  CreatedAt: DateTimeOffset
  CreatedByUserId: Guid?
}

LfsObjectCopy {
  RepositoryId: Guid
  Oid: string
  StorageNodeId: Guid
  Kind: enum             // Plaintext | Encrypted
  StoredAt: DateTimeOffset
  LastVerifiedAt: DateTimeOffset?   // last time an inventory saw it on disk
}
```

A row in `LfsObject` means the object reached LFS Quorum. `LfsObjectCopy` is bookkeeping for routing and dashboards. The reconciler trusts what is on disk, not this table.

**Batch planning:**
- *Download:* each object with an `LfsObject` row gets a download action. Any other object gets a per-object `404` error.
- *Upload:* each object with an `LfsObject` row gets no actions (the client skips it). Any other object gets upload and verify actions, after these checks:
  - the size is at most `Lfs:MaxObjectBytes` (else per-object `422`);
  - the sum of the sizes of objects not yet present, plus `StorageBytesUsed`, is within the repository's resolved byte limit (else `507` for the whole batch);
  - the repository has at least one healthy encrypted replica (else `503`: "LFS requires encrypted replication; this repository is still migrating").

**Verify = quorum replication.** On verify, the API:
1. Checks that the primary holds the object at the declared size (`HEAD`).
2. Streams it from the primary, encrypts it, and uploads it to every healthy encrypted replica in parallel. Each upload is capped by `Lfs:ReplicaUploadTimeoutSeconds`, so one replica that hangs only costs itself.
3. As soon as the primary and ≥1 encrypted replica hold the object: inserts the `LfsObject` and `LfsObjectCopy` rows in one transaction, adds the size to the repository's storage usage, and answers `200`.
4. Afterwards, in a DI scope of its own, finishes the second encrypted copy and the plaintext copy on the read replica (when the read replica is on a different node). Whatever stays behind, the reconciler repairs.

If quorum cannot be reached, verify answers `503` with `Retry-After` and nothing is recorded. The push fails on the client; pushing again re-runs upload and verify (the primary already holds the bytes and accepts the repeated upload without change). Verify is idempotent: a second verify of a recorded object answers `200` at once. Concurrent verifies of the same object are serialized by the unique key; the loser answers `200` once the winner has committed.

**Pre-receive integrity gate:**
- The storage pre-receive hook finds the LFS pointer blobs the push introduces (blobs ≤ 1024 bytes that parse as LFS pointers, in commits the push adds) and sends their `oid`, `size` and path to API push validation alongside the existing commit data.
- The API rejects the push if any oid has no `LfsObject` row for the repository: `LFS object <oid> (<path>) is missing on the server. Run: git lfs push --all origin <ref>`.
- Pointers already in history before the push are not re-checked. Web merges create no new pointer blobs and are unaffected.

**Quota:** LFS sizes are added to `StorageBytesUsed` once per object (logical size, not copy count). Repository usage reports `GitBytes` and `LfsBytes` separately. The per-repository `MaxFileSize` push rule keeps applying to git blobs only; an LFS pointer is tiny and passes. LFS objects are capped by `Lfs:MaxObjectBytes`.

**Repository deletion:** deletes the `LfsObject` and `LfsObjectCopy` rows and calls the per-repository LFS `DELETE` on every node that held a copy. Directories left behind for repositories that no longer exist are removed by the reconciler.

### Reconciler, rebalance and recovery

- **New reconciler step "LFS copies",** running after the existing steps, per repository, each repository and node failing on its own:
  1. Fetch the LFS inventory of every plaintext and encrypted node of the repository.
  2. Compare it with the repository's `LfsObject` set. Update `LastVerifiedAt` for copies found; delete `LfsObjectCopy` rows for copies not found.
  3. Repair, longest-missing first:
     - missing encrypted copy → re-encrypt from a plaintext copy;
     - missing plaintext copy on the primary or read replica → copy from the other plaintext node, or decrypt from an encrypted replica when no plaintext copy exists.
  4. Log `LFS object <oid> of <repository> missing on <node>` and the repair result.

  This follows the lesson from on-disk artifact loss (#230): decisions come from what is on disk, not from what the database believes.
- **New replicas:** a replica added by rebalance or RF4 backfill is filled by the same repair path; no separate copy job.
- **Hot promotion** does not wait for LFS completeness. After promotion, the API schedules an immediate LFS repair for the repository. Downloads of objects not restored yet answer `503` with `Retry-After` (see dispatcher).
- **Cold recovery:** after the bare repository is reconstructed and verified, the API restores the repository's LFS objects from the encrypted replica (decrypt, verify the oid, stream to the new plaintext nodes). The repository becomes writable as soon as git is restored; the LFS restore continues in the background, because the encrypted copies stay intact meanwhile. The admin dashboard shows the restore progress.

### Web UI and content reads

- The storage blob content endpoint recognizes pointer blobs (exact pointer format, ≤ 1024 bytes) and returns `lfs: { oid, size }` with the blob. The API adds `available` (whether an `LfsObject` row exists).
- The blob viewer shows a "Stored with Git LFS" badge, the real size and a download button; image types are previewed inline. A missing object shows "LFS object not available". No inline rendering of LFS text files in v1.
- The raw endpoint returns the object content for pointer blobs, streamed from a read target through the API with the correct `Content-Type` and `Content-Length`, under the existing content-read authorization.
- Repository usage shows git bytes and LFS bytes separately.
- The admin replication dashboard shows per repository: LFS object count, bytes, and copies missing per node.

### Configuration

| Key | Default | Meaning |
|---|---|---|
| `Lfs:Enabled` | `false` until rollout completes | Batch API answers `501` when off |
| `Lfs:MaxObjectBytes` | 2 GiB | Per-object cap (still limited by remaining quota) |
| `Lfs:MaxBatchObjects` | 100 | Objects per batch request (git-lfs default batch size) |
| `Lfs:ActionTokenLifetimeSeconds` | 3600 | Validity of an action token at transfer start |
| `Lfs:ReplicaUploadTimeoutSeconds` | 600 | Cap for one encrypted replica upload during verify |
| `Lfs:SegmentBytes` | 1 MiB | AEAD segment size for new encrypted copies |

The 2 GiB default keeps a verify (stream, encrypt, upload) within HAProxy's 5-minute server inactivity timeout on the internal network. Raising it requires checking that timeout.

### Rollout

1. API first, with `Lfs:Enabled=false`. It accepts the new push-validation fields, so old and new hooks keep working.
2. Storage nodes one at a time, the primary last (existing `--full` procedure). The new endpoints are inert until used, and the new pre-receive hook sends pointer data that the API already understands.
3. Dispatchers.
4. Enable `Lfs:Enabled`.

Rollback in reverse: disable first. Already recorded objects stay on disk and in the database and are served again once LFS is re-enabled.

---

## Testing Decisions

### What makes a good test

Tests assert externally observable behavior: what the real `git-lfs` client sees, LFS status codes and error bodies per the spec, bytes downloaded equal bytes uploaded, objects surviving node loss, and no plaintext on encrypted nodes. Tests do not assert ciphertext layout, internal call order or temp-file names, except where a security boundary is the behavior (tampered segment rejected, wrong role refused).

### Modules and layers

| Module | Layer | Focus | Prior art |
|---|---|---|---|
| LFS path parsing and routing (dispatcher) | `OpenGitBase.Dispatcher.Tests` | All five paths parsed; non-LFS paths unchanged; body limit equals declared size; streaming without buffering; download fallback order; `503` when no target holds a recorded object | `GitSmartHttpPathParser` / handler tests |
| Batch planning, action tokens, quota (API) | `OpenGitBase.Api.Tests` + feature tests | Download/upload actions by presence; per-object `404`/`422`; batch `507`, `413`, `503` (no encrypted replica); token scope and expiry; read vs write permission; private repository refusal | Access-check controller tests; `ControllerCoverageTests` / `QueryHandlerCoverageTests` gates |
| Segmented AEAD | `OpenGitBase.Common.Tests` (or API) | Round-trip at 0, 1, segment-size±1 and multi-segment lengths; rejects tamper, truncation, reorder, cross-object and cross-repository swap, wrong key version; plaintext hash check | Encrypted artifact service tests |
| Verify / quorum orchestration | `OpenGitBase.Api.Tests` | `200` with one encrypted ack; `503` and nothing recorded with none; idempotent re-verify; concurrent verify of one oid; async read-replica copy scheduled; quota usage increased once | Quorum-replicate tests |
| Storage object store | `test_storage_http_server.py` | Streaming put with hash or size mismatch rejected; idempotent put; atomic visibility; role enforcement; inventory; per-repository delete; objects survive `import-bundle` of the same repository | Existing artifact endpoint tests |
| Pre-receive pointer detection | `test_storage_push_validation.py` + API push-validation tests | Pointers detected only in new commits; non-pointer small blobs ignored; push rejected with the missing oid and path; accepted when recorded | Push rule tests |
| LFS reconciler step | `AntiEntropyReconcilerService` tests | On-disk loss repaired while the DB says healthy; encrypted re-created from plaintext; plaintext restored from encrypted; one failing node does not block others | #230 artifact-presence tests |
| Cold recovery / promotion | `ColdRecoveryService` / promotion tests | LFS restored and oid-verified after cold recovery; repository writable before the LFS restore finishes; repair scheduled after hot promotion | Existing recovery tests |
| Web blob view | Vitest + Playwright visual snapshots | Pointer parsing; badge, size, download, image preview, "not available" state | Blob viewer snapshots |

### Compose E2E (`git-lfs` installed in the E2E runner)

Prior art: `PatPushCloneTests`, `HaStorageChaosTests`, `LargeRepositoryReplicationTests`.

Required scenarios:

- Track, push and clone with the real client: working-tree files are byte-identical; a re-push uploads nothing.
- Push without uploading the objects (pointer committed, objects withheld) → rejected with the missing oid; after `git lfs push --all` → accepted.
- Object above `Lfs:MaxObjectBytes` → clear client error; quota exceeded → `507` message.
- Large object (> 100 MB) end to end, so the streaming paths are exercised.
- Encrypted replica nodes contain no plaintext of an uploaded object (search the node's disk for a known plaintext marker).
- Primary+read node lost → cold recovery → clone + `git lfs pull` returns identical bytes.
- Encrypted LFS directory deleted on one replica while the DB says healthy → reconciler restores it.
- Read replica lacks an object → download falls back to the primary.
- Repository deleted → LFS directories gone on every node.

---

## Out of Scope

- LFS over SSH (`git-lfs-authenticate`, `git-lfs-transfer`). SSH users set `lfs.url` to the HTTPS endpoint.
- The file locking API beyond the `/locks/verify` stub (`git lfs lock` reports locking as unsupported).
- Transfer adapters other than `basic` (multipart, tus, resumable uploads).
- Direct client-to-object-store transfers (presigned URLs) and S3-compatible backends.
- Garbage collection of LFS objects no longer referenced by any ref. Objects stay until the repository is deleted.
- LFS content in CI Workspace Materialization. Jobs see pointer files in v1.
- LFS-aware diffs in merge requests and the commit view (the pointer diff is shown).
- LFS badges in tree listings.
- Download bandwidth accounting or bandwidth quotas.
- Server-side conversion of existing large blobs to LFS (`git lfs migrate` stays client-side).
- Deduplication of identical objects across repositories (objects are stored per repository, under per-repository keys).

---

## Further Notes

### Relationship to other PRDs

- Lifts the LFS non-goals in [git-storage-proxy.md](git-storage-proxy.md), [ha-storage-replication.md](ha-storage-replication.md), [git-https-personal-access-tokens.md](git-https-personal-access-tokens.md) and [repository-web-browsing.md](repository-web-browsing.md).
- Builds on [encrypted-replica-storage.md](encrypted-replica-storage.md) (#13): roles, Repository Key, quorum definition and cold recovery. LFS adds a content-addressed path next to the watermark-based bundle path and does not change it.

### Why this is simpler than git replication

LFS objects never change and are named by their content. A copy is either correct (its SHA-256 matches its name) or missing. There is no ordering between objects, no watermark to compare, no epoch fence, and no split-brain: two nodes can never hold different versions of the same oid. Every repair is "copy what is missing from any good copy".

### Risk and effort

The protocol work (dispatcher routes, batch planning, web UI) is small and follows existing patterns. The risk sits in the durability work: quorum on verify, the reconciler step, cold recovery and the pre-receive gate. **LFS must not be enabled in production before those land.** An LFS that only stores objects on the primary would lose them silently on failover, and clones would then fail with "object not found" — worse than no LFS.

### Suggested slice order

1. `lfs-01` Storage node object store: streaming put/get with hash check, role enforcement, inventory, delete.
2. `lfs-02` Segmented AEAD format and its tests.
3. `lfs-03` `LfsObject` model, batch planning, action tokens, quota checks (API).
4. `lfs-04` Dispatcher routes: batch, upload, download, verify, `locks/verify` stub.
5. `lfs-05` Quorum replication on verify, async read-replica and second-encrypted copies.
6. `lfs-06` Pre-receive integrity gate.
7. `lfs-07` Reconciler LFS step, filling new replicas.
8. `lfs-08` Cold recovery and post-promotion LFS restore.
9. `lfs-09` Web UI: pointer detection, badge, download, image preview, raw endpoint, usage split.
10. `lfs-10` Repository deletion cleanup, admin dashboard figures.
11. `lfs-11` Compose E2E with `git-lfs`, including chaos scenarios.

Enable `Lfs:Enabled` in production only after slices 1–8 and 11.

---

## Update: slice breakdown

The "Suggested slice order" above is replaced by nine vertical slices. Each slice delivers something that runs end to end; the segmented AEAD is part of lfs-03, and the storage, API and dispatcher groundwork is part of the lfs-01 tracer bullet.

| ID | Slice | Type | Blocked by |
|----|-------|------|------------|
| lfs-01 | #235 Tracer bullet: git lfs push and clone round trip | AFK | — |
| lfs-02 | #236 Permissions, size limits, batch cap and quota | AFK | #235 |
| lfs-03 | #237 Encrypted quorum on verify | AFK | #235 |
| lfs-04 | #238 Pre-receive integrity gate for LFS pointers | AFK | #235 |
| lfs-05 | #239 Reconciler repair from on-disk LFS inventory | AFK | #237 |
| lfs-06 | #240 Cold recovery and post-promotion LFS restore | AFK | #239 |
| lfs-07 | #241 Web UI: LFS files in blob view and raw downloads | AFK | #235, #236 |
| lfs-08 | #242 Repository deletion cleanup and admin LFS figures | AFK | #239 |
| lfs-09 | #243 LFS chaos E2E and production rollout | HITL | #237–#242 |

`Lfs:Enabled` stays off in production until lfs-09.
