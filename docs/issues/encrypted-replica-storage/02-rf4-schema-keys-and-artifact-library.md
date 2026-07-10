# RF=4 schema, repository keys, and artifact library

## Metadata

- ID: ers-02
- Type: AFK
- Status: ready
- Source: docs/prd/encrypted-replica-storage.md

## Parent

[PRD: Encrypted Replica Storage](../../prd/encrypted-replica-storage.md)

## What to build

Introduce the replication control-plane schema extensions and two new deep modules testable in isolation.

**Schema extensions:**

```
RepositoryReplica.Role: Primary | ReadReplica | EncryptedReplica

Repository {
  ReadReplicaStorageNodeId: Guid    // denormalized routing helper
  ReplicationState: ... | Rf4Migrating | Rf4Healthy | Recovering
}

RepositoryReplica {
  ArtifactWatermark: long?           // encrypted replicas only
}

RepositoryKey {
  RepositoryId: Guid
  KeyCiphertext: string             // envelope-encrypted
  KeyVersion: int
  CreatedAt: DateTimeOffset
}
```

**Repository Key Service** — generate per-repo symmetric key on create; store envelope-encrypted; issue ephemeral key material to authenticated primary callers only; never persist plaintext outside request scope.

**Encrypted Artifact Service** — create git bundle from bare repo path; AES-256-GCM encrypt/decrypt with associated data `{repoId}:{watermark}:{epoch}`; build and verify manifest (epoch, watermark, bundle hash, key version).

## Acceptance criteria

- [ ] EF migration adds four replica roles, RF=4 replication states, RepositoryKey table, and artifact metadata fields
- [ ] Repository Key Service unit tests cover generate, envelope round-trip, ephemeral issuance, and rejection of non-primary callers
- [ ] Encrypted Artifact Service unit tests cover bundle encrypt/decrypt round-trip, AEAD tamper rejection, and manifest mismatch detection
- [ ] Key rotation stub exists (interface only; implementation deferred)
- [ ] Existing RF=3 repositories continue to operate unchanged until backfill (no breaking migration)

## Blocked by

- None — can start immediately

## User stories covered

- 18 — As an administrator, I want repository encryption keys managed independently from repository contents, so that key rotation and recovery policies can evolve separately.
- 19 — As an administrator, I want repository encryption keys envelope-encrypted by the platform, so that keys are never stored in plaintext outside trusted runtime memory.
- 20 — As an administrator, I want repository encryption keys to support future rotation, so that key lifecycle can be managed without re-architecting storage.
- 21 — As an administrator, I want repository keys never derived from user passwords, so that credential rotation does not affect repository decryption.
- 22 — As a platform operator, I want the encryption layer independent from Git internals, so that artifact format can evolve without modifying Git.
- 23 — As a security reviewer, I want authenticated encryption with integrity verification on artifacts, so that corrupted or tampered replicas are detectable.

## Notes

Reuse existing platform DataKey envelope pattern (same approach as email and fleet credential encryption). No storage-layer changes in this slice.
