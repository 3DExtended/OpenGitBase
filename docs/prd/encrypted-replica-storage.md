<!-- forge: #13 -->

# PRD: Encrypted Replica Storage

## Problem Statement

OpenGitBase currently replicates every repository as a plaintext bare Git copy on three storage nodes. Any storage node hosting a replica can read the full contents of a private repository. Transport between nodes is protected with mutual TLS and platform credentials are encrypted at rest, but repository data itself remains unencrypted on every replica.

This creates several problems:

- Compromise of any replica node exposes all repositories hosted on that node.
- Storage providers and node operators must be fully trusted.
- Distributed replication increases attack surface because each additional replica can read repository contents.
- Organizations with strict security requirements cannot use third-party or community-contributed storage without trusting their operators.
- There is no path for organizations to self-host authoritative Git storage while still participating in platform-wide durability guarantees.

The goal is to reduce trust required for replica nodes, enable organization-contributed storage capacity, and preserve Git functionality — while maintaining durability guarantees and converging public and private repositories onto a single replication pipeline.

---

## Solution

Replace today's RF=3 plaintext-only replication with a unified **four-copy model** for all repositories (public and private):

- **1× Write Primary** — authoritative bare Git repository; performs all Git write operations.
- **1× Read Replica** — plaintext bare Git repository dedicated to serving Git fetch/clone and web content reads.
- **2× Encrypted Replicas** — immutable, versioned encrypted replication artifacts; no Git operations, no plaintext repository.

Primary and read replica may colocate on the same storage node. Encrypted replicas must not colocate with each other or with the primary+read pair.

**Write quorum:** push succeeds only after the primary and at least one encrypted replica confirm receipt of the encrypted artifact. Read replica sync and the second encrypted replica catch up asynchronously.

**Key management:** each repository receives a unique symmetric Repository Key, envelope-encrypted by the API control plane. Storage nodes never receive plaintext keys.

**Recovery:** if the primary fails and the read replica is in sync, hot-promote the read replica to primary (existing epoch semantics). If both plaintext copies are lost, enter controlled cold recovery: obtain Repository Key, decrypt encrypted artifacts, reconstruct bare repository, verify integrity, then resume writes.

**Organization-contributed storage:** organizations may register their own storage nodes to expand capacity and earn higher byte limits. Each node has a configurable maximum capacity and a per-node hosting scope (`OwnOrgOnly` or `CrossOrgAllowed`). Cross-org hosting always stores encrypted artifacts only.

**Self-host tiers** (org placement settings, inheritable per org with per-repo override):

| Org nodes provided | Layout |
|---|---|
| 0 (default) | Primary + read on platform nodes; encrypted replicas prefer cross-org nodes, fallback to platform when community capacity exhausted |
| 1 | Primary + read on org node; 2 encrypted elsewhere |
| 2 | Primary + read on one org node; 1 encrypted on second org node; 1 encrypted elsewhere |
| 3 | All 4 copies on org nodes (primary+read colocated on one, 1 encrypted each on the other two) |

**Delivery phasing:**

1. **Phase 1** — Four-copy encrypted replication on platform fleet only; background migration from RF=3.
2. **Phase 2** — Organization storage node registration, self-host tiers, per-node capacity, quota credits.
3. **Phase 3** — Cross-org community hosting, capacity-aware placement algorithm, per-repo byte limit overrides for fully self-hosted repos.

```
Git Client
     │
     ▼
Dispatcher
     │
     ├── writes ──► Write Primary (plaintext bare git)
     │
     └── reads  ──► Read Replica (plaintext bare git)
                           │
                           │ post-receive: produce encrypted artifact
                           ▼
                   Encrypted Replication Layer
                           │
              ┌────────────┴────────────┐
              ▼                         ▼
      Encrypted Replica A       Encrypted Replica B
      (artifacts only)          (artifacts only)
```

---

## User Stories

### Durability and replication

1. As a repository owner, I want my repository replicated without exposing plaintext data to untrusted storage nodes, so that third-party operators cannot read my code.
2. As a repository owner, I want push to succeed only after at least one encrypted replica confirms receipt, so that acknowledged pushes survive loss of the primary+read node pair.
3. As the system, I want the read replica and second encrypted replica to catch up asynchronously after quorum replication, so that push latency is not blocked on all four copies.
4. As the system, I want each repository to maintain a monotonic replication watermark tied to encrypted artifact confirmation, so that in-sync status and recovery decisions use an exact signal.
5. As the system, I want the primary to commit watermarks only after encrypted quorum replication succeeds, so that watermarks always represent durably replicated state.
6. As a security reviewer, I want encrypted artifact upload authenticated with existing storage node credentials and mTLS, so that replication does not widen trust boundaries.
7. As the system, I want read replica synchronization to use git-native fetch from the primary, so that the read copy remains a valid bare repository.
8. As a repository owner, I want two encrypted replicas maintained for every repository, so that durability survives loss of any single encrypted node.
9. As an enterprise customer, I want to use infrastructure I do not fully trust without allowing storage operators to read repository contents.
10. As a security-conscious customer, I want compromise of an encrypted replica node to reveal no repository contents.

### Git operations and routing

11. As a developer pushing changes, I want write operations routed exclusively to the write primary, so that replication orchestration has a single writer.
12. As a developer cloning or fetching, I want read operations routed to the dedicated read replica, so that read load is separated from write traffic.
13. As a developer browsing repository content on the web, I want reads served from the read replica, so that web traffic does not add load to the write primary.
14. As a developer fetching after a successful push, I want to see pushed commits from the read replica once it catches up, so that normal workflows remain transparent.
15. As a git client, I want push to fail clearly when encrypted quorum cannot be met, so that I know the system refused to ack without durability rather than losing data silently.
16. As a dispatcher, I want the access-check response to include the primary address and read replica address, so that I can route reads and writes without additional API round-trips.
17. As a storage node holding an encrypted replica, I want Git operations rejected on encrypted artifact storage, so that the node cannot accidentally serve repository contents.

### Encryption and key management

18. As an administrator, I want repository encryption keys managed independently from repository contents, so that key rotation and recovery policies can evolve separately.
19. As an administrator, I want repository encryption keys envelope-encrypted by the platform, so that keys are never stored in plaintext outside trusted runtime memory.
20. As an administrator, I want repository encryption keys to support future rotation, so that key lifecycle can be managed without re-architecting storage.
21. As an administrator, I want repository keys never derived from user passwords, so that credential rotation does not affect repository decryption.
22. As a platform operator, I want the encryption layer independent from Git internals, so that artifact format can evolve without modifying Git.
23. As a security reviewer, I want authenticated encryption with integrity verification on artifacts, so that corrupted or tampered replicas are detectable.

### Recovery and failover

24. As the system, I want to hot-promote the read replica to primary when it is in sync and the primary becomes unhealthy, so that git operations resume quickly when one plaintext copy survives.
25. As the system, I want cold recovery from encrypted replicas when both plaintext copies are lost, so that durability is preserved even when the primary+read node fails.
26. As an administrator, I want repository recovery procedures to be deterministic and auditable, so that disaster recovery can be reviewed and repeated.
27. As an administrator, I want recovery to verify repository integrity before resuming writes, so that corrupted artifacts cannot produce a writable but incorrect repository.
28. As an administrator, I want recovery to fail safely when integrity verification fails, so that the system does not silently serve corrupted data.
29. As the system, I want epoch guards to reject stale primary writes after promotion or recovery, so that split-brain is prevented.
30. As a git client, I want clone/fetch to resume from the read replica or promoted primary after failover, so that read availability recovers when a plaintext copy exists.
31. As a git client, I want push to resume after recovery completes and encrypted quorum is reachable, so that write availability returns following cold recovery.

### Organization-contributed storage

32. As an organization owner, I want to register my own storage nodes with the platform, so that I can self-host authoritative Git storage.
33. As an organization owner, I want to configure each registered node as hosting only my org's repos or opt in to hosting other orgs' repos, so that I control my node's exposure.
34. As an organization owner with one storage node, I want primary and read replica on my node with encrypted copies elsewhere, so that no plaintext infrastructure I do not own holds my repository data.
35. As an organization owner with two storage nodes, I want primary+read on one node and one encrypted copy on my second node, so that I maximize self-hosting while meeting durability requirements.
36. As an organization owner with three storage nodes, I want all four copies on my nodes, so that my repository is fully self-hosted.
37. As an organization owner, I want org-level placement defaults inherited by new repos with per-repo override, so that fleet configuration is manageable.
38. As an organization owner hosting nodes for other orgs, I want those copies always encrypted, so that I cannot read other organizations' repository contents.
39. As an organization owner, I want contributed storage capacity to increase my organization's byte limit, so that hosting nodes provides tangible benefit.
40. As a platform administrator, I want to configure maximum capacity per platform storage node, so that fleet capacity is bounded and predictable.
41. As an organization owner with more than three storage nodes, I want to configure higher per-repo byte limits for repos fully hosted on my nodes, so that large repositories are supported when I provide sufficient capacity.

### Placement and fleet operations

42. As the API assigning a new repository, I want storage node selection based on available capacity, repository count, and max bytes per repository, so that placement avoids overcommit.
43. As the API, I want encrypted replica placement to prefer cross-org nodes when available, so that durability is geographically and organizationally distributed.
44. As the API, I want encrypted replica placement to fall back to platform nodes when community capacity is exhausted, so that repository creation never fails solely due to community shortage.
45. As an operator, I want unhealthy encrypted replicas replaced automatically, so that RF=4 durability is maintained without manual intervention.
46. As an operator, I want a periodic reconciler to repair lagging read replicas and missing encrypted artifacts, so that silent drift is corrected.
47. As an operator, I want per-repository replication state visible in the admin UI including four-copy roles and migration progress, so that fleet health is auditable.
48. As a storage node, I want to report applied watermarks and artifact watermarks in heartbeat payloads, so that the API can detect lag and in-sync status.
49. As a platform operator, I want replication failures to continue participating in quorum calculations and attention signals, so that existing observability semantics are preserved.
50. As a platform operator, I want background reconciliation jobs to operate with encrypted replication artifacts, so that anti-entropy extends to the new model.

### Migration and unified pipeline

51. As an operator upgrading from RF=3, I want existing repositories migrated to the four-copy model via background backfill, so that git keeps working during migration.
52. As a repository owner, I want no user-visible downtime during migration from RF=3 to RF=4, so that upgrades are transparent.
53. As a developer, I want public and private repositories to use the same replication pipeline, so that the system has one code path to maintain.
54. As a tester, I want integration tests covering push quorum, read routing, hot promotion, cold recovery, and migration backfill, so that behavior is regression-protected.

### Local development

55. As a developer running locally, I want the default three-node compose fleet to exercise four-copy replication with primary+read colocated on one node and encrypted copies on the other two, so that local behavior matches production invariants.
56. As a developer, I want repository creation to fail with a clear error when insufficient healthy nodes exist for four-copy placement, so that local behavior matches production constraints.

### HTTPS git and web browsing

57. As a developer using git over HTTPS, I want the same primary/read routing and encrypted quorum write behavior as SSH, so that transport choice does not weaken replication guarantees.
58. As a web user browsing a repository, I want content reads served from the read replica with a lag banner when appropriate, so that browsing behavior matches today's replica routing semantics.

---

## Implementation Decisions

### Delivery phasing

Work is delivered in three phases. Each phase produces a shippable increment. Phases must not be skipped in production rollout order.

**Phase 1 — Platform four-copy encryption.** Four replica roles, Repository Key envelope encryption, encrypted git bundle replication, write quorum on encrypted confirmation, hot read promotion, cold recovery, RF=3→RF=4 background backfill. All copies initially on platform-operated nodes.

**Phase 2 — Organization storage node registration.** Org-contributed nodes, self-host tiers, per-node MaxBytes and HostingScope, org quota credits, org/repo placement settings.

**Phase 3 — Cross-org community hosting.** Capacity-aware placement algorithm, cross-org encrypted placement, per-repo byte limit overrides for fully self-hosted repos on orgs with more than three nodes.

### Major modules

Six new deep modules and eight extended modules encapsulate behavior behind narrow interfaces testable in isolation.

#### 1. Replica Set Planner (extend, API-side)

**Interface:** given healthy storage nodes, org placement settings, and optional exclusion list, return `{ primaryNodeId, readReplicaNodeId, encryptedNodeIdA, encryptedNodeIdB }` or none if placement cannot satisfy colocation and capacity constraints.

Pure function over registry state plus org settings. Generalizes existing three-node selection to four roles with colocation rules:

- Primary and read replica may share a node.
- Encrypted replicas must be on distinct nodes from each other and from the primary+read pair (unless fleet size requires compromise — document minimum fleet size).

Phase 1: all platform nodes. Phase 2: incorporate org-owned nodes per self-host tier. Phase 3: prefer cross-org nodes for encrypted slots.

#### 2. Repository Key Service (new, API-side)

**Interface:**

```
GenerateKey(repositoryId) → keyVersion
GetEphemeralKey(repositoryId, callerPrimaryNodeId) → keyMaterial  // authenticated, short-lived
GetWrappedKey(repositoryId) → ciphertext  // recovery use only, trusted runtime
RotateKey(repositoryId) → keyVersion  // Phase 1 stub; future rotation
```

Generates per-repository symmetric keys on repo create. Stores envelope-encrypted in Postgres using existing platform DataKey pattern (same approach as email and fleet credential encryption). Never persists plaintext outside request-scoped memory. Primary receives ephemeral key material over authenticated internal API during replication only.

#### 3. Encrypted Artifact Service (new, shared library)

**Interface:**

```
CreateBundle(repoPath) → bundleBytes
Encrypt(bundleBytes, key, associatedData) → aeadBlob
Decrypt(aeadBlob, key, associatedData) → bundleBytes
Verify(manifest, aeadBlob, key) → ok | integrityError
BuildManifest(epoch, watermark, bundleHash, keyVersion) → manifest
```

v1 artifact format:

```
{repoId}/{watermark}/manifest.json   # epoch, watermark, bundle sha256, key version
{repoId}/{watermark}/bundle.aead     # AES-256-GCM ciphertext of git bundle
```

Associated authenticated data: `{repoId}:{watermark}:{epoch}`.

Primary produces full bundle per watermark (not incremental). Incremental packfile artifacts deferred to future work.

#### 4. Replication Control Plane (extend API / Postgres)

Owns durable four-copy state, epochs, watermarks, promotion, recovery, and rebalance orchestration. Single source of truth for dispatchers and storage agents.

**Conceptual schema extensions:**

```
StorageNode {
  ...existing fields...
  OwnerOrganizationId: Guid?           // null = platform-operated
  MaxBytes: long
  UsedBytes: long                       // reported or computed
  HostingScope: enum                   // OwnOrgOnly | CrossOrgAllowed
}

Repository {
  ...existing fields...
  PrimaryStorageNodeId: Guid
  ReadReplicaStorageNodeId: Guid        // denormalized for fast routing; also in RepositoryReplica
  ReplicationEpoch: long
  PrimaryWatermark: long
  ReplicationState: enum               // Rf3Healthy | Rf4Migrating | Rf4Healthy | Degraded | Promoting | Recovering
  PlacementPolicy: enum?              // inherits org default; self-host tier override
  MaxBytesOverride: long?              // Phase 3; only when fully self-hosted
}

RepositoryReplica {
  RepositoryId: Guid
  StorageNodeId: Guid
  Role: enum                           // Primary | ReadReplica | EncryptedReplica
  AppliedWatermark: long
  LastSyncedAt: DateTimeOffset?
  ArtifactWatermark: long?              // encrypted replicas only
  BackfillState: enum
}

RepositoryKey {
  RepositoryId: Guid
  KeyCiphertext: string                // envelope-encrypted
  KeyVersion: int
  CreatedAt: DateTimeOffset
}

OrganizationStorageSettings {
  OrganizationId: Guid
  DefaultPlacementPolicy: enum
  DefaultSelfHostPreference: enum      // PlatformOnly | PreferSelfHost | RequireSelfHost
}
```

#### 5. Quorum Replicate Orchestrator (extend, API-side)

**Interface:** unchanged external contract (`quorum-replicate` from primary post-receive), changed internal semantics.

Validates caller is primary. Requires encrypted artifact confirmation from at least one encrypted replica at `PrimaryWatermark + 1`. Commits watermark atomically with epoch guard. Triggers async: read replica git sync, second encrypted replica upload.

Quorum members for watermark commit: primary + ≥1 encrypted replica (not read replica).

#### 6. Cold Recovery Service (new, API-side)

**Interface:**

```
BeginRecovery(repositoryId) → recoveryId
ReconstructFromEncrypted(repositoryId, targetPrimaryNodeId, targetReadNodeId) → ok | failed
VerifyReconstruction(repositoryId) → ok | integrityError
CompleteRecovery(recoveryId) → routes updated
```

Workflow when both plaintext copies lost:

1. Set `ReplicationState = Recovering`.
2. Obtain Repository Key from envelope store.
3. Select encrypted replica with highest artifact watermark.
4. Decrypt bundle, reconstruct bare repository on target nodes.
5. Run integrity verification (`git bundle verify`, ref manifest comparison).
6. Assign primary and read roles, bump epoch, set `Rf4Healthy`.
7. Audit log entire workflow.

Failure at any step leaves repo in `Recovering` or `Degraded`; writes remain blocked.

#### 7. RF=4 Backfill Service (new, API-side background)

**Interface:** periodic scan for `Rf3Healthy` repositories; migrate to four-copy layout.

Per repository:

1. Designate read replica from existing non-primary copy (highest applied watermark).
2. Provision encrypted artifact storage on two nodes.
3. Produce initial encrypted bundle from primary.
4. Transition through `Rf4Migrating` → `Rf4Healthy`.

Git operations continue on existing copies during migration. Follows same background service pattern as existing RF=1→RF=3 backfill.

#### 8. Storage Agent / HTTP Server (extend, storage layer)

New internal endpoints for encrypted artifact storage:

```
PUT  /internal/repos/{id}/artifacts/{watermark}   // receive encrypted blob + manifest
GET  /internal/repos/{id}/artifacts/{watermark}   // recovery fetch (authenticated API caller only)
DELETE /internal/repos/{id}/artifacts/{watermark} // delete quorum extension
```

Encrypted replica nodes store artifacts under dedicated path (not bare `.git`). Git HTTP and SSH endpoints reject requests for repos where the node's role is `EncryptedReplica`.

Modified post-receive hook flow:

1. Fetch replication context from API.
2. Request ephemeral Repository Key from API.
3. Create git bundle, encrypt, upload to encrypted replica(s).
4. Call API quorum-replicate with encrypted confirmation.
5. Async: git fetch to read replica.

#### 9. Read/Write Routing (extend, API + dispatcher)

Writes route to primary only. Reads route to read replica; fallback to primary if read replica unhealthy or lagging beyond threshold. Access-check response exposes `{ primary, readReplica, replicationEpoch, writeQuorumAvailable }`.

Web content reads use read replica via existing content client pattern. Encrypted replicas never appear in routing targets.

#### 10. Promotion Handler (extend, API-side)

On primary failure:

- If read replica in sync (`AppliedWatermark == PrimaryWatermark`): hot-promote using existing epoch increment semantics. Read role moves to new node; regenerate encrypted artifacts from new primary.
- If read replica not in sync or also lost: trigger Cold Recovery Service.

Encrypted replicas are never hot-promoted to serve Git.

#### 11. Organization Storage Node Registry (new, Phase 2)

**Interface:**

```
RegisterOrgNode(organizationId, enrollmentToken, maxBytes, hostingScope) → nodeId
UpdateNodeCapacity(nodeId, maxBytes)
UpdateHostingScope(nodeId, scope)
ListOrgNodes(organizationId) → nodes
```

Extends existing storage node enrollment with org ownership and capacity fields. Platform admin manages platform nodes; org admin manages org-contributed nodes.

#### 12. Capacity Placement Engine (new, Phase 3)

**Interface:**

```
ScoreNode(node, repoMaxBytes) → fitScore
SelectEncryptedSlots(repoOwnerOrgId, excludeNodeIds) → [nodeId, nodeId]
```

Considers: free bytes, current repository count, configured max bytes per repository, hosting scope, org ownership. Encrypted placement prefers `CrossOrgAllowed` nodes owned by a different organization; falls back to platform nodes.

Per-repo byte override permitted when all four copies reside on org-owned nodes and org operates more than three nodes.

### Storage trust model

Two trust classes for repository copies:

**Trusted plaintext tier (Primary, ReadReplica):**

- Stores normal bare Git repository.
- Performs Git operations (primary: read/write; read replica: read-only sync).
- May only colocate primary+read on same node.
- Must be owned by repo org or platform for cross-org repos.

**Untrusted encrypted tier (EncryptedReplica):**

- Stores encrypted replication artifacts only.
- Cannot perform Git operations.
- Cannot browse repository contents.
- Cannot independently become a Git server.
- Required for any copy on a node owned by a different organization.

### Git responsibility

Git functionality on the trusted tier remains unchanged:

- receive-pack, upload-pack (primary only)
- reference updates, garbage collection, pack generation (primary only)
- git fetch sync (primary → read replica)
- Protected branch and push rule enforcement at primary pre-receive hook

Encrypted replicas never execute Git synchronization.

### Replication protocol

Replication to encrypted replicas uses encrypted artifact upload, not git fetch. Artifacts are immutable and versioned by the existing watermark mechanism. Each watermark maps to one encrypted bundle representing repository state at that point.

Read replica continues git-native fetch sync from primary (async, not quorum-gating).

### Envelope encryption

Repository Keys protected separately from repository contents:

- Enables future key rotation, customer-managed keys, external KMS integration.
- Platform never stores Repository Keys in plaintext outside trusted runtime memory.
- v1 uses platform DataKey; interface designed for KMS provider swap.

### High availability semantics

Existing concepts retained where applicable:

- Monotonic watermarks and epochs
- Write quorum (redefined: primary + encrypted)
- Anti-entropy reconciliation
- Background rebalance for unhealthy nodes
- Automatic node replacement

**Changed semantics:**

- Loss of primary no longer instantly produces another writable Git server unless read replica is in sync.
- Total loss of both plaintext copies blocks reads and writes until cold recovery completes.
- Durability is preserved via encrypted replicas; availability profile differs from today's hot-replica model.

### Failure semantics

| Event | Behavior |
|---|---|
| Encrypted replica lost | No impact on availability; rebalance replaces encrypted copy |
| Read replica lost | Reads fall back to primary; rebalance restores read replica |
| Primary lost, read in sync | Hot-promote read replica; brief promotion window |
| Primary + read lost (colocated) | Cold recovery from encrypted; reads and writes blocked until complete |
| Encrypted artifact corrupted | Integrity verification fails; recovery selects alternate encrypted replica |
| Push with encrypted quorum unavailable | Push fails; primary rolls back local watermark |

### Security model

**Trusted:** API, dispatcher, current write primary, read replica (when owned by repo org or platform), recovery workflow runtime.

**Untrusted:** Encrypted replica nodes (including platform-operated encrypted copies), cross-org community nodes, node operators.

Compromise of an encrypted replica must not expose repository contents. Compromise of platform API still exposes keys (accepted TCB boundary for v1).

### Assumptions

- Minimum platform fleet size for Phase 1 is three nodes (primary+read colocated, two encrypted on separate nodes).
- Full git bundle per watermark is acceptable for v1 latency and storage overhead; optimization deferred.
- Organizations registering storage nodes complete the same PKI enrollment as platform nodes.
- Quota credit formula (bytes contributed → org limit increase) will be defined in Phase 2 implementation planning.
- Repository metadata (merge requests, discussions, repo names) remains unencrypted in Postgres.

---

## Testing Decisions

### What makes a good test

Tests verify externally observable behavior at existing HA seams — not encryption implementation internals. Assert on Git outcomes, routing decisions, watermark progression, and recovery results. Do not assert on cipher text layout, key derivation steps, or internal service call ordering unless verifying a security boundary rejection.

### Modules to test

| Module | Test focus |
|--------|------------|
| Replica Set Planner | Role assignment, colocation rules, self-host tiers, insufficient capacity |
| Repository Key Service | Key generation, envelope round-trip, ephemeral issuance auth, storage node rejection |
| Encrypted Artifact Service | Bundle encrypt/decrypt round-trip, AEAD tamper rejection, manifest mismatch |
| Quorum Replicate Orchestrator | Push ack gated on encrypted confirmation; read replica async; watermark monotonicity |
| Cold Recovery Service | Reconstruction produces identical refs/objects; integrity failure aborts; audit trail |
| Promotion Handler | Hot read promote with epoch bump; cold path when both plaintext lost |
| RF=4 Backfill Service | Migration from RF=3 without breaking push; state transitions |
| Read/Write Routing | Writes → primary; reads → read replica; encrypted excluded from targets |
| Storage Agent | Encrypted node rejects git ops; artifact upload/download auth |
| Capacity Placement Engine (Phase 3) | Cross-org preference; same-org exclusion; platform fallback |

### Prior art

Follow patterns from existing HA storage replication tests:

- Push quorum and watermark commit integration tests
- Primary promotion and epoch rejection tests
- Rebalance and anti-entropy service tests
- Admin replication status API tests
- E2E chaos scenarios for node failure and recovery (extend for four-copy and cold recovery cases)

### Required recovery scenarios

- Primary failure with in-sync read replica (hot promote)
- Primary + read colocated node failure (cold recovery)
- Encrypted replica corruption (integrity rejection, alternate replica used)
- Missing encrypted artifact at expected watermark
- Invalid encryption metadata / wrong key version
- Interrupted recovery (resumable or safely failed)
- Repeated recovery attempts (idempotent)
- Migration backfill mid-push (no data loss)
- Cross-org placement never stores plaintext on foreign org node (Phase 3)

---

## Out of Scope

- End-to-end encrypted Git where no server can decrypt repository contents.
- Fully homomorphic encryption.
- Encrypted execution of Git operations.
- Confidential computing (Intel SGX, AMD SEV, ARM CCA).
- Customer-managed KMS integration (future; envelope design supports it).
- Incremental encrypted packfile artifacts (v2 optimization).
- Changes to Git protocol behavior visible to clients.
- Encryption of merge requests, discussions, or repository metadata in Postgres.
- PostgreSQL transparent data encryption.
- Redis encryption.
- Transport-layer security improvements covered by existing security work.

---

## Further Notes

### Relationship to HA Storage Replication PRD

This PRD supersedes the replication model in [ha-storage-replication.md](ha-storage-replication.md) for new work. RF=3 plaintext semantics remain during migration until RF=4 backfill completes. The watermark, epoch, quorum-orchestration, and rebalance patterns from RF=3 are reused; only replica roles, quorum composition, and replication payload change.

### Availability vs durability trade-off

The current architecture provides hot replicas that can immediately assume the primary role. The four-copy model provides encrypted cold replicas that require reconstruction when both trusted plaintext copies are lost. This trades a modest increase in recovery time for substantial reduction in trust placed on distributed storage nodes — while preserving a hot read replica for normal read scaling and fast primary failover when one plaintext copy survives.

### Foundation for future work

The encryption layer and org-contributed storage model establish foundations for:

- Customer-managed encryption keys and external KMS providers
- Encrypted off-site backups
- Confidential-computing deployments where artifact decryption occurs in attested enclaves
- Marketplace incentives for community storage providers

### Local development topology

Default three-node compose fleet maps to: primary+read colocated on storage-1, encrypted replicas on storage-2 and storage-3. Phase 3 cross-org placement falls back to this layout when no org-contributed nodes exist.
