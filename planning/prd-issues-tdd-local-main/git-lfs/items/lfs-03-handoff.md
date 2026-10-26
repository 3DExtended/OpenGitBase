# lfs-03 handoff — Encrypted quorum on verify

- PRD: `docs/prd/git-lfs.md` (#234); slice: `docs/issues/git-lfs/03-encrypted-quorum-on-verify.md` (#237)
- Dependencies: lfs-01 (done)
- Branch: `claude/lfs-implementation-difficulty-1a3fd2`

## Codebase facts

- Repository Key: `IRepositoryKeyService.TryGetRepositoryKeyAsync(repoId)` → `EphemeralRepositoryKey(byte[] KeyMaterial, int KeyVersion)`
  (32 bytes, version 1 today). Today the *primary storage node* encrypts git bundles (key sent to the primary via
  `create-replication-artifact`); encrypted replicas never see it. For LFS the API encrypts — the key goes nowhere.
- Existing AES-GCM: `EncryptedArtifactService` (whole-buffer, `nonce||tag||ct`, AAD `{repo}:{watermark}:{epoch}`) —
  not reusable for streaming; LFS gets its own segmented format.
- Replica discovery: `RepositoryReplicationRoutingQuery` excludes encrypted replicas; use
  `GetRepositoryReplicationContextQuery { RepositoryId, StorageNodeId = primary }` → `Peers` (all replicas incl. the
  caller: `StorageNodeId, InternalHost, InternalHttpPort, Role, IsHealthy`).
- API → storage: `http://{InternalHost}:{InternalHttpPort}/internal/...` + Bearer node token
  (`GetStorageNodeApiTokenQuery`). Existing artifact upload is hex-in-JSON in memory — LFS streams raw bytes.
- Background work pattern: `ReplicaCatchUpService.StartInBackground(IServiceScopeFactory, Func<T, Task>)`
  (`Task.Run` + `CreateAsyncScope`, `CancellationToken.None`).
- Storage roles: `ROLE_DIR/{repoId}.txt` ∈ Primary|Replica|ReadReplica|EncryptedReplica; data volume
  `/var/lib/opengitbase` is persistent (artifacts under `/var/lib/opengitbase/artifacts`).
- No `Retry-After` anywhere yet.
- Compose: storage-1 primary (+ read replica), storage-2/3 encrypted replicas; E2E repos reach `Rf4Healthy`.

## Design

- `LfsObjectCipher` (API): header `OLFS | v1 | keyVersion i32 | segmentBytes i32 | plaintextSize i64 | noncePrefix 8B`,
  then per segment `ciphertext || tag(16)`; nonce = prefix(8) || BE u32 index; AAD
  `{repositoryId:D}:lfs:{oid}:{index}:{isFinal}`; 0-byte objects = one empty final segment. Decrypt checks header,
  key version, final flag, trailing bytes, total size and SHA-256 = oid.
- Storage: encrypted store `STORAGE_LFS_ENCRYPTED_ROOT` (default `/var/lib/opengitbase/lfs`) `{repo}/aa/bb/{oid}.aead`;
  8081 routes `HEAD/GET/PUT /internal/lfs/{repo}/encrypted/{oid}` (EncryptedReplica only, else 403) and
  `HEAD/GET/PUT /internal/lfs/{repo}/objects/{oid}` (403 on EncryptedReplica).
- API: `LfsReplicationService` resolves the replica set, streams primary GET → cipher → encrypted PUT per replica in
  parallel (each capped by `Lfs:ReplicaUploadTimeoutSeconds`), returns at first confirmation; background completion
  (own DI scope) awaits the rest, retries missing encrypted copies, copies plaintext to a distinct read replica,
  records `LfsObjectCopy` rows. Verify records `LfsObject` + copies in one transaction; 503 + `Retry-After` otherwise.
- Batch upload without a healthy encrypted replica → 503 migration message. Download transfer targets: read replica,
  then primary.
