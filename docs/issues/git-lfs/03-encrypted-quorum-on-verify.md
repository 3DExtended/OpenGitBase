<!-- forge: #237 -->

# [slice] lfs-03 — Encrypted quorum on verify

## Metadata

- Type: AFK
- Status: ready

## Parent

PRD discussion #234

## What to build

Make an acknowledged LFS upload as durable as an acknowledged push. Verify succeeds only after the write primary **and** at least one encrypted replica hold the object. The Repository Key never leaves the API.

1. **Segmented AEAD (API, C#).**
   - Encryption uses fixed plaintext segments (`Lfs:SegmentBytes`, default 1 MiB), each AES-256-GCM with the Repository Key.
   - Nonce = random 64-bit per-object prefix + 32-bit segment counter.
   - Associated data = `{repositoryId}:lfs:{oid}:{segmentIndex}:{isFinal}`.
   - A header records format version, key version, segment size and plaintext size.
   - Encryption and decryption both stream. Decryption verifies the SHA-256 of the result against the oid before anything is written.
2. **Encrypted object store (storage node).** `.../lfs/{repositoryId}/{oid[0:2]}/{oid[2:4]}/{oid}.aead` under a root on the persistent data volume that is separate from the per-watermark artifact tree. Streaming `PUT`/`GET`/`HEAD` on the internal HTTP port.
3. **Role enforcement.** Plaintext endpoints refuse on a node that holds only the encrypted role for the repository; encrypted endpoints refuse on the primary or the read replica.
4. **Verify = quorum.**
   - The API streams the object from the primary, encrypts it, and uploads it to every healthy encrypted replica in parallel. Each upload is capped by `Lfs:ReplicaUploadTimeoutSeconds`.
   - As soon as one encrypted replica confirms: insert `LfsObject` + `LfsObjectCopy` rows in one transaction, then answer `200`.
   - Quorum not reached → `503` with `Retry-After`, nothing recorded.
   - Verify of an already-recorded object → `200` at once.
5. **`LfsObjectCopy`** entity and migration: `RepositoryId`, `Oid`, `StorageNodeId`, `Kind` (`Plaintext` | `Encrypted`), `StoredAt`, `LastVerifiedAt`.
6. **Async completion.** In a DI scope of its own: the second encrypted copy, and a plaintext copy to the read replica when it is on another node (streamed through the API).
7. **No encrypted replica.** A repository with no healthy encrypted replica gets `503` on upload batches: "LFS requires encrypted replication; this repository is still migrating".
8. **Read routing.** Downloads stream from the read replica first and fall back to the primary when the read replica lacks the object.

Demo: push an LFS file with one encrypted replica stopped — the push succeeds. Stop both encrypted replicas — the push fails with a clear message. Grep the encrypted nodes' disks for a known plaintext marker — no match.

## Acceptance criteria

- [ ] AEAD round-trips at 0 bytes, 1 byte, segment size ±1 and multi-segment sizes
- [ ] AEAD rejects a tampered segment, a truncated object, reordered segments, a segment from another object or repository, and a wrong key version
- [ ] Verify answers `200` only when the primary and ≥1 encrypted replica hold the object; otherwise `503` and no row
- [ ] One hanging encrypted replica does not delay quorum beyond its own timeout
- [ ] The Repository Key is never sent to a storage node for LFS (API test on outgoing requests)
- [ ] The second encrypted copy and the read-replica copy appear asynchronously and get `LfsObjectCopy` rows
- [ ] Batch download never offers an object that has not reached quorum
- [ ] Repository without a healthy encrypted replica: upload batch `503` with the migration message
- [ ] Downloads come from the read replica and fall back to the primary on `404`
- [ ] Storage tests: role enforcement for plaintext and encrypted endpoints
- [ ] Compose E2E: no plaintext of an uploaded object on encrypted replica disks

## Blocked by

- #235

## User stories covered

- 16, 17, 18, 19, 20, 21, 23, 24
