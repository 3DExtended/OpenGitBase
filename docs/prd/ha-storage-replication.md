# PRD: HA Storage Replication (RF=3)

## Problem Statement

OpenGitBase distributes repositories across multiple storage nodes for capacity, but each repository is assigned to exactly one node for its lifetime. When that node becomes unhealthy, git operations for affected repositories fail fast even though other storage nodes may have spare capacity. There is no replication of bare repository data across nodes, so the loss of a storage node and its volume means permanent loss of every repository that lived only on that node.

For operators, a single storage node failure is therefore both an availability incident and a potential data-loss incident. Self-hosted deployments need repository data to survive the permanent loss of any single storage node without manual recovery from backups, while keeping the existing dispatcher → storage routing model and per-node `/srv/git` volumes.

## Solution

Introduce **RF=3 replication**: every repository exists as a full bare git copy on three distinct storage nodes (one primary and two replicas). The API remains the control plane — it owns replica-set membership, primary assignment, per-repo replication watermarks, and epoch-based write leases. Writes use **quorum 2/3**: a push succeeds only after the primary and at least one replica have durably stored the new pack and ref updates; the third replica catches up asynchronously. Reads may be served by the primary or any replica marked **in sync** (applied watermark matches primary watermark).

On primary failure, the API automatically promotes the replica with the highest watermark, bumps the repo epoch, and updates dispatcher routing. When any node becomes unhealthy or is deregistered, automatic rebalance assigns a replacement node and backfills missing copies without operator intervention; if the node recovers before backfill completes, the original trio is restored. Existing single-node repositories are upgraded via a background backfill job. Local and production deployments require at least three healthy storage nodes before new repositories can be created; the default Docker Compose stack is expanded to three storage services.

**Priority ordering (confirmed):** data loss is unacceptable; git operation failure when quorum cannot be met is acceptable; availability during node failure is desirable but secondary.

## User Stories

### Durability and replication

1. As a repository owner, I want my repository data stored on three independent storage nodes, so that the permanent loss of any single node does not destroy my repository.
2. As a git client pushing changes, I want push to succeed only after at least two of three replicas have durably stored the new objects and refs, so that an acknowledged push cannot be lost to a single-node failure.
3. As the system, I want the third replica to catch up asynchronously after a quorum write, so that push latency is not blocked on all three nodes while RF=3 is eventually restored.
4. As the system, I want each repository to maintain a monotonic replication watermark, so that in-sync status and promotion decisions are based on an exact signal rather than time estimates.
5. As the system, I want the primary to bump the watermark only after quorum replication succeeds, so that watermarks always represent durably replicated state.
6. As a security reviewer, I want storage-to-storage replication authenticated with per-node mTLS using the existing PKI, so that peer sync does not widen the dispatcher SSH trust boundary.
7. As the system, I want peer replication to use git-native mechanisms (internal fetch/sync of bare repos), so that every copy remains a valid bare repository on disk.

### Repository lifecycle

8. As a repository owner creating a repository, I want bare repos provisioned on all three nodes before the API returns success, so that a new repository never exists as a single copy even briefly.
9. As the API, I want new repository creation blocked when fewer than three storage nodes are healthy, so that RF=3 is enforced from birth in all environments including local development.
10. As a repository owner deleting a repository, I want deletion confirmed on at least two nodes before the database record is removed, so that deleted repositories do not leave authoritative orphans while still allowing delete when one node is down.
11. As the system, I want the third node's copy removed asynchronously after a quorum delete, so that delete succeeds under the same 2/3 policy as push.
12. As an operator, I want a periodic reconciler to detect and repair missing copies, stale watermarks, and orphaned on-disk repos, so that silent replication drift is corrected without waiting for the next push.

### Read and write routing

13. As a developer cloning or fetching, I want read operations routed to the primary or any in-sync replica, so that read load can spread without serving stale refs from a lagging replica.
14. As a developer pushing, I want write operations always routed to the current primary, so that replication orchestration has a single writer.
15. As a dispatcher, I want the access-check response to include the primary address and the set of read-eligible replica addresses, so that I can route reads and writes without additional API round-trips.
16. As a git client fetching after a successful push, I want to see the pushed commits whether I hit the primary or an in-sync replica, so that read routing transparency holds for normal workflows.
17. As a git client, I want push to fail clearly when fewer than two replicas are reachable, so that I know the system refused to ack without quorum rather than losing data silently.

### Failover and promotion

18. As the system, I want the API to automatically promote the most up-to-date replica when the primary becomes unhealthy, so that git operations can resume without operator action.
19. As the system, I want each repository to carry a monotonic epoch/generation counter, so that split-brain writes from a stale primary are rejected after promotion.
20. As a dispatcher, I want routing to update after promotion via the normal access-check path, so that failover does not require dispatcher-local state.
21. As a git client, I want clone/fetch to resume from a promoted primary or in-sync replica after failover, so that read availability recovers automatically.
22. As a git client, I want push to resume once a new primary is elected and quorum is reachable, so that write availability recovers without manual intervention when two nodes remain healthy.

### Replica placement and fleet operations

23. As the API assigning a new repository, I want the primary chosen as the healthy node with the most free disk space and replicas chosen as the next two distinct healthy nodes by the same signal, so that capacity-weighted spread extends the existing selection logic to RF=3.
24. As an operator adding storage capacity, I want new nodes to participate in replica-set assignment for new repositories without manual per-repo configuration, so that scaling storage remains self-registration driven.
25. As an operator, I want unhealthy nodes automatically replaced in affected replica sets, so that a dead node does not leave repositories permanently at RF=2 without human action.
26. As an operator, I want explicit node deregistration to trigger the same rebalance pipeline as heartbeat failure, so that graceful and ungraceful removal behave consistently.
27. As the system, I want a recovered node reattached to its original replica sets when replacement backfill has not yet reached in-sync status, so that transient outages do not permanently reshuffle trios.
28. As the system, I want a recovered node to enter spare capacity when replacement backfill already reached in-sync status, so that flip-flopping is avoided after meaningful recovery work completed.
29. As the system, I want rebalance to never treat a repository as safe with fewer than two durable copies, so that automatic healing does not sacrifice the data-loss bar.

### Migration and observability

30. As an operator upgrading from single-node assignment, I want existing repositories backfilled to RF=3 in the background from their current primary, so that git keeps working during migration.
31. As an operator, I want per-repository replication state visible in the admin UI (for example: backfilling, RF=3 healthy, degraded, promoting), so that fleet health is auditable without reading logs.
32. As a storage node, I want to report per-repository applied watermarks in heartbeat payloads, so that the API can mark replicas in-sync for read routing and detect lag.
33. As the API, I want to expose replication health on repository admin/detail surfaces, so that operators can see when a repo is below RF=3 or missing quorum.
34. As a tester, I want integration tests that verify push quorum, read routing to in-sync replicas, primary promotion, and automatic rebalance, so that HA behavior is regression-protected.

### Local development and deployment

35. As a developer running locally, I want the default Docker Compose stack to include three storage nodes, so that RF=3 behavior is exercised without extra setup.
36. As a developer running locally, I want bootstrap and enrollment scripts to provision PKI and enrollment for the third storage node, so that local fleet bootstrap stays one command.
37. As a developer, I want repository creation to fail with a clear error when fewer than three storage nodes are healthy in compose, so that local behavior matches production invariants.

### HTTPS git (existing transport)

38. As a developer using git over HTTPS, I want the same primary/read-replica routing and quorum write behavior as SSH, so that transport choice does not weaken replication guarantees.

## Implementation Decisions

### Major modules

The work extends the existing multi-node storage architecture from the Git Storage Proxy PRD. Six new deep modules and five extended modules encapsulate HA behavior behind narrow interfaces.

#### 1. Replica Set Planner (new, API-side)

**Interface:** given the set of healthy storage nodes and optional exclusion list, return `{ primaryNodeId, replicaNodeIdA, replicaNodeIdB }` or none if fewer than three healthy distinct nodes.

Pure function over registry state; unit-testable. Generalizes `StorageNodeSelection.SelectBestNode` to pick primary as max free bytes, then the next two distinct nodes by the same ordering. Used at repository creation and during rebalance when selecting a replacement node (replacement must not already hold two copies of the same repo unless unavoidable in tiny fleets).

#### 2. Replication Control Plane (extend API / Postgres)

Owns durable replica-set state, epochs, watermarks, and promotion/rebalance orchestration. Single source of truth for dispatchers and storage agents.

**Conceptual schema extensions:**

```
Repository {
  ...existing fields...
  PrimaryStorageNodeId: Guid          // FK; replaces semantic sole use of StorageNodeId as "the only node"
  ReplicationEpoch: long              // bumped on promotion; stale primary rejected
  PrimaryWatermark: long              // bumped after each successful quorum write
  ReplicationState: enum              // e.g. RF1Backfilling | RF3Healthy | Degraded | Promoting
  // StorageNodeId retained for compatibility during migration or deprecated in favor of primary
}

RepositoryReplica {
  RepositoryId: Guid
  StorageNodeId: Guid
  Role: enum                          // Primary | Replica
  AppliedWatermark: long
  IsInSync: bool                      // derived: AppliedWatermark == Repository.PrimaryWatermark
  LastSyncedAt: DateTimeOffset?
  BackfillState: enum                 // None | InProgress | Complete | Failed
}

StorageNode {
  ...existing fields...
  // optional: fleet role Spare | Active when recovered node not in any trio
}

RebalanceJob / BackfillJob (conceptual queue or state machine rows) {
  RepositoryId, SourceNodeId?, TargetNodeId, Reason, Status, StartedAt, CompletedAt
}
```

**Responsibilities:**
- Persist trio membership at repository create (all three `RepositoryReplica` rows + primary).
- Increment `PrimaryWatermark` atomically with epoch checks after quorum write confirmation.
- Run primary promotion: select highest `AppliedWatermark` among replicas, increment `ReplicationEpoch`, swap primary FK, update replica roles.
- Enqueue and track rebalance/backfill jobs when nodes become unhealthy or deregistered.
- Gate repository create on `healthyNodeCount >= 3`.

#### 3. Quorum Lifecycle Orchestrator (new, API-side)

**Interface:**
- `CreateRepositoryWithReplication(model)` → provision on all 3 nodes, persist RF=3 state, or fail with rollback
- `DeleteRepositoryWithReplication(repo)` → quorum 2/3 delete on nodes, then DB delete, enqueue async third scrub
- Coordinates with Storage Provisioner Client and new Peer Replication Coordinator (below); does not implement git sync itself

**Create order:** plan trio → provision bare repo on all three synchronously → persist repository + replica rows → return success. Roll back provisioned copies on any failure.

**Delete order:** resolve trio → delete on primary and best-effort second node until 2 successes → remove DB row → async third scrub + reconciler safety net.

#### 4. Quorum Write Coordinator (new, storage-side + API confirmation)

**Interface (conceptual):** after primary completes `git-receive-pack`, `ReplicatePushQuorum(repositoryId, packRefSummary)` → sync to peers over mTLS → return when ≥2 nodes (including primary) report durable apply + watermark.

Primary storage node:
1. Runs receive-pack locally with fsync policy unchanged from today.
2. Invokes peer sync to at least one other replica via mTLS git-native channel.
3. Waits for peer acknowledgment of applied watermark.
4. Calls API to commit watermark increment (API validates epoch + quorum evidence).
5. Returns success to dispatcher; initiates async catch-up to third replica.

API rejects watermark bumps from non-primary or stale epoch.

#### 5. Storage Peer Replicator (new, repo-storage-layer)

**Interface:**
- `SyncRepositoryFromPeer(sourceHost, repositoryPhysicalPath, expectedWatermark)` → mTLS git-native sync (internal smart HTTP or git fetch) → update local bare repo → fsync → report applied watermark
- `ProvisionBareRepo(physicalPath)` — existing, unchanged surface
- `DeleteBareRepo(physicalPath)` — existing, unchanged surface

Uses existing per-node certificates (`node.crt`, `node.key`) for **mTLS between storage peers**. Does not reuse dispatcher SSH identity. Validates peer identity against API-provided or cert-pinning registry.

Internal endpoints or git smart HTTP routes are scoped to replication operations only — not arbitrary shell.

#### 6. Primary Failover Manager (new, API-side background worker)

**Trigger:** primary node's `IsHealthy` false for configured threshold (reuse storage heartbeat miss logic).

**Behavior:**
1. Load repository replica set; if primary unhealthy, select promoting replica (max `AppliedWatermark`, tie-break by node id).
2. Transaction: `ReplicationEpoch++`, move primary role, mark old primary row as replica or evicted.
3. Emit metric/log; dispatchers pick up new routing on next access check.
4. Enqueue rebalance job to add replacement node for evicted slot if node is permanently gone.

Does not ack client pushes during promotion window; clients retry.

#### 7. Automatic Rebalance & Reattach Coordinator (new, API-side background worker)

**Triggers:**
- Storage node unhealthy beyond threshold (same as failover trigger)
- Explicit storage node deregistration/delete

**Behavior:**
- For each affected repository, if trio would drop below 3 members, select replacement via Replica Set Planner (exclude dead node, prefer nodes not already holding two copies).
- Start backfill from current primary to replacement via Storage Peer Replicator path (API orchestrates token/mTLS metadata).
- **Reattach (confirmed):** if dead node recovers before replacement reaches in-sync, cancel in-flight replacement and restore original trio membership.
- **If replacement reached in-sync before recovery:** keep replacement in trio; recovered node marked spare for future assignments.
- Never mark repository `RF3Healthy` while fewer than two copies exist with matching watermarks.

#### 8. Anti-Entropy Reconciler (new, API-side scheduled job)

**Interface:** `RunFleetReconciliation()` on configurable interval (default 15–60 minutes).

Per repository:
- Verify bare repo exists on all trio members (or active replacement).
- Compare `AppliedWatermark` vs `PrimaryWatermark`; trigger backfill for lagging replicas.
- Scrub on-disk orphans not present in DB (from failed async deletes).
- Detect repos stuck in `RF1Backfilling` / `Degraded` and re-enqueue jobs.

Read-only with respect to git clients; repair actions are idempotent.

#### 9. Repository Access Check Routing (extend existing module)

Extended allowed response:

```
{
  allowed: true,
  repositoryId: Guid,
  physicalPath: "/srv/git/{id}.git",
  primary: { internalHost, internalSshPort, internalGitHttpPort },
  readTargets: [ { internalHost, ports..., role: "primary"|"replica" } ],
  replicationEpoch: long,
  ...existing fields...
}
```

**Read target selection (API-side or dispatcher-side):** include primary always; include replicas where `IsInSync == true`.

**Deny reasons (extended):**
- Storage unavailable (no read targets)
- Write quorum unavailable (push requested, fewer than two trio members healthy)
- Repository migrating/backfilling (optional: allow reads from primary during backfill, deny writes until RF=3 — confirm in implementation; default: reads from primary during backfill, writes require RF=3)

#### 10. Git Session Proxy (extend dispatcher module)

**Interface extensions:**
- `ProxyReadSessionAsync` → pick one read target (primary preferred, else random/round-robin among in-sync replicas from access check)
- `ProxyWriteSessionAsync` → always primary from access check

SSH and HTTPS smart HTTP paths both use the same access-check routing fields.

#### 11. RF=1 → RF=3 Backfill Worker (new, API-side background worker)

For repositories with only `StorageNodeId` / single replica row from pre-HA era:
1. Mark `ReplicationState = RF1Backfilling`.
2. Plan two additional replicas via Replica Set Planner.
3. Provision bare repo on new nodes; git-sync from existing primary.
4. Insert `RepositoryReplica` rows; when all three watermarks match, mark `RF3Healthy`.

Git operations continue on original primary during backfill. New repos require RF=3 immediately and do not use this path.

#### 12. Storage Node Runtime (extend repo-storage-layer)

**Extend storage agent heartbeat payload:**
- Per-repo `{ repositoryId, appliedWatermark }` list (or delta since last heartbeat for scale)
- Backfill/replication progress indicators

**Extend internal HTTP or add mTLS git replication listener:**
- Peer sync endpoints authenticated via mTLS
- Optional: API-triggered `POST /internal/repos/{id}/sync` for backfill orchestration

### Architectural decisions (confirmed)

| Decision | Choice |
|----------|--------|
| Replication factor | RF=3 (primary + 2 replicas) |
| Write quorum | 2/3 before client ack |
| Third replica catch-up | Async after ack |
| Data loss priority | Unacceptable — overrides availability |
| Git ops when quorum lost | Fail with clear error |
| Replication mechanism | Primary receive-pack → git-native mTLS peer sync → watermark commit |
| Read routing | Primary or any in-sync replica (watermark match) |
| Write routing | Primary only |
| Control plane | API + Postgres (epochs, watermarks, promotion, rebalance) |
| Create provisioning | All 3 nodes synchronously before API success |
| Delete | Quorum 2/3 on nodes, then DB delete, async third scrub |
| In-sync signal | Per-repo replication watermark |
| Existing repos | Background backfill to RF=3 |
| Minimum healthy nodes for create | 3 (all environments; no dev RF=1 override) |
| Local compose | Expand to 3 storage nodes + bootstrap/PKI |
| Peer auth | mTLS via existing per-node PKI |
| Node removal / failure | Automatic rebalance (no manual drain required) |
| Rebalance triggers | Unhealthy threshold + explicit deregistration |
| Node recovery | Reattach if replacement not in-sync; else spare capacity |
| Reconciliation | Periodic anti-entropy job |
| Split-brain prevention | Per-repo `ReplicationEpoch`; stale primary writes rejected |

### API contracts (new or extended)

**Storage node heartbeat (extended, storage → API):**
- Add per-repo applied watermarks (or summarized lag counts)
- Response may include pending backfill/replication instructions (optional pull model)

**Replication watermark commit (storage primary → API, after quorum sync):**
- Request: repository id, epoch, new watermark, quorum node ids that confirmed
- Response: ack / stale epoch / not primary

**Repository access check (extended, dispatcher → API):**
- Add primary, readTargets array, replicationEpoch
- Deny push when write quorum unavailable

**Storage peer replication (storage ↔ storage, mTLS):**
- Git smart HTTP or internal sync protocol scoped to known physical paths
- Mutual cert validation against fleet PKI

**Admin replication status (API → UI):**
- Per-repo: primary, replicas, watermarks, state enum, active rebalance/backfill jobs
- Per-node: repositories where node is primary/replica, spare status

### Docker and fleet bootstrap

- Add `storage-3` service mirroring `storage-1` / `storage-2` (volume, PKI cert/key mounts, enrollment env).
- Update dispatcher `depends_on` to include `storage-3`.
- Extend `scripts/bootstrap-fleet.sh` to generate `storage-3` enrollment and PKI if not present.
- Document that repository creation requires three healthy nodes; two-node compose is insufficient for new repos post-feature.

### Relationship to Git Storage Proxy PRD

This PRD **supersedes** the following out-of-scope items from the Git Storage Proxy PRD:
- "Automatic failover or re-routing to a different storage node when the assigned node fails"
- "Repository migration between storage nodes"
- "Replication or geo-redundancy"

It preserves: dispatcher proxy model, UUID physical paths, per-node internal HTTP provisioning, access-check routing as the dispatcher's source of truth, and storage node self-registration.

`StorageNodeId` on `Repository` may remain as alias for `PrimaryStorageNodeId` during migration or be migrated via a schema migration.

## Testing Decisions

### What makes a good test here

Test observable behavior at module boundaries:
- Given fewer than three healthy nodes, repository create is rejected
- Given three healthy nodes, repository create leaves bare repos on all three paths before DB row exists
- Given a successful push, at least two nodes report matching watermark and a third may lag
- Given push with one replica down but quorum reachable, push succeeds; with two down, push fails without watermark bump
- Given primary failure, promotion selects highest applied watermark and increments epoch
- Given stale epoch primary attempting watermark commit, API rejects
- Given access check with one in-sync replica, read routing includes primary and that replica
- Given access check during degraded quorum, push denied and fetch allowed from in-sync targets
- Given node unhealthy, rebalance job enqueued; given node recovery before in-sync, original trio restored
- Given delete with one node down, quorum delete succeeds and DB row removed
- Reconciler repairs missing third copy when simulated drift injected

Prefer unit tests for planner, promotion selection, watermark logic, and epoch guards; integration tests for storage peer mTLS sync and end-to-end git push/clone/failover through compose.

### Modules to test

| Module | Test type | Prior art |
|--------|-----------|-----------|
| Replica Set Planner | Unit | `StorageNodeSelection` tests |
| Quorum Lifecycle Orchestrator | Handler/integration tests with mocked storage HTTP | `CreateRepositoryWithStorageQueryHandler` tests |
| Replication Control Plane (promotion, epoch) | Unit/handler tests | Storage node heartbeat handler tests |
| Watermark commit / in-sync derivation | Unit | Storage node registry handler tests |
| Access check routing extension | API controller tests | `RepositoryAccessChecksControllerTests` |
| Git Session Proxy read/write split | Unit | Dispatcher git smart HTTP / SSH session tests |
| Storage Peer Replicator | Shell integration in repo-storage-layer | Existing `integration-test.sh` |
| Automatic Rebalance & Reattach | Handler/worker tests with timed heartbeats | Storage node health transition tests |
| Anti-Entropy Reconciler | Unit with seeded drift | Repository delete orchestration tests |
| RF=1 backfill worker | Integration | Create + provision tests |
| End-to-end RF=3 | Compose integration (stretch) | Git storage proxy e2e patterns |

### Recommended test priority

1. Replica Set Planner and create gate (<3 nodes)
2. Quorum create (all 3 provisioned) and quorum delete (2/3)
3. Watermark increment guards (epoch, primary-only)
4. Promotion selects correct replica; epoch bumps
5. Access-check readTargets and write quorum deny
6. Storage peer mTLS sync integration test
7. Reattach vs spare-capacity rebalance scenarios
8. Reconciler drift repair
9. Full compose: push, kill primary, push again, clone from replica

## Out of Scope

- Geo-redundancy, cross-region replica placement, or operator placement rules (region/rack/tier tags)
- Configurable replication factor per repository (RF=2 dev mode, RF=5 enterprise tier)
- RF=2 degraded mode for new repositories when only two nodes are healthy
- Manual drain-then-remove as the only path for node decommission (automatic rebalance is in scope; manual override may be added later)
- Separate Raft/etcd coordinator service outside the API
- Storage nodes re-evaluating user authentication during git operations
- Full git object-graph hash comparison for reconciliation (watermark + existence checks only in v1)
- Read load-balancing algorithms beyond primary-preferred plus in-sync replicas
- Git LFS object replication (standard bare repo refs/objects only)
- Parametric Docker Compose generator for N storage nodes (fixed third node added; dynamic N remains follow-up)
- Live `StorageBytesUsed` quota accounting from pack sizes on replicated pushes
- Cross-tenant storage deduplication or erasure coding
- Client-visible replication topology in clone URLs (routing remains internal)

## Further Notes

### Assumptions

- HTTPS git transport reuses the extended access-check response; no separate routing logic per transport.
- Existing per-node enrollment and certificate thumbprint registration continue; mTLS peer sync uses the same PKI material already mounted in storage containers.
- `StorageNodeId` on repositories created before this feature remains the backfill source primary until `RF3Healthy`.
- Heartbeat interval and unhealthy thresholds reuse existing configuration unless operationally insufficient for promotion/rebalance; tune during implementation.
- Push latency increases due to synchronous peer sync; acceptable given durability priority.

### Suggested implementation order

1. Schema migration: `RepositoryReplica`, watermark/epoch fields, replication state
2. Replica Set Planner + create gate (≥3 healthy nodes)
3. Quorum create (provision all 3) and extend provisioner orchestration
4. Storage Peer Replicator (mTLS git-native sync) + watermark reporting in heartbeat
5. Quorum write coordinator on primary + API watermark commit
6. Access-check and dispatcher read/write routing split
7. Primary Failover Manager
8. RF=1 backfill worker for existing repositories
9. Quorum delete + async third scrub
10. Automatic Rebalance & Reattach Coordinator
11. Anti-Entropy Reconciler
12. Docker compose + bootstrap script storage-3 expansion
13. Admin UI replication status
14. Integration and e2e tests

### Open questions for implementation (not blocking PRD)

- Exact mTLS sync transport: internal git smart HTTP on `STORAGE_INTERNAL_GIT_HTTP_PORT` vs dedicated replication port — prefer reusing git HTTP infrastructure with peer cert verification.
- Whether dispatcher or API selects among multiple read-eligible targets (recommend dispatcher: primary first, else round-robin among replicas for load spread).
- Backfill-in-progress write policy: allow writes after primary+one new replica sync or wait for full RF=3 before writes (recommend: reads from primary during backfill; writes enabled once quorum path exists on new trio members).
- Heartbeat payload size when many repos per node — may require delta/report-changed-only optimization in a follow-up if heartbeats become large.
