<!-- forge: #16 -->

# PRD: Git Storage Proxy & Multi-Node Storage

## Problem Statement

OpenGitBase already authenticates SSH git clients at the dispatcher and authorizes them against the API, but authorized sessions end without executing any git operation. Users cannot clone, pull, fetch, or push repository data. Meanwhile, bare git repositories are not provisioned on disk when a repository is created in the product, and there is no model for distributing repositories across multiple storage nodes or routing git traffic to the correct node.

Operators must also manually duplicate Docker Compose service blocks to add storage capacity, and storage nodes are currently reachable on host ports — inconsistent with the goal of making storage reachable only through dispatchers.

## Solution

Complete the git path end-to-end: after the dispatcher authorizes a session, it proxies the git wire protocol to the correct storage node. Storage nodes hold bare repositories at stable UUID-based paths, register themselves with the API via heartbeat, and expose a private internal HTTP API for provisioning and deletion. The API assigns each new repository to the healthy storage node with the most free disk space at creation time, provisions the bare repo synchronously, and returns routing metadata to dispatchers during access checks. Storage nodes trust the private network and a dispatcher-only SSH identity — they do not re-evaluate user authentication or authorization.

## User Stories

1. As a developer, I want `git clone ssh://git@host/{owner}/{repo}` to return real repository data, so that I can use standard git workflows against OpenGitBase.
2. As a developer, I want `git pull` and `git fetch` to work against an existing remote, so that I can stay up to date with remote changes.
3. As a developer, I want `git push` to work when I have write access, so that I can publish commits to OpenGitBase.
4. As a repository owner, I want a bare repository created on disk when I create a repository in the UI, so that the first push or clone succeeds immediately.
5. As a repository owner, I want the on-disk repository removed when I delete a repository in the product, so that storage is not wasted on orphaned data.
6. As a git client, I want to use `{ownerSlug}/{repoSlug}` in the SSH URL, so that remotes are human-readable and match the web UI.
7. As the system, I want slug-to-repository resolution to remain in the API, so that storage nodes only deal with stable UUID filesystem paths.
8. As an organization member, I want `ssh://git@host/{orgSlug}/{repoSlug}` to work for organization-owned repositories, so that org repos behave like user-owned repos from git's perspective.
9. As a dispatcher, I want the access-check response to include the storage node address and canonical physical path, so that I can proxy without additional lookups.
10. As a storage node, I want to register with the API on startup, so that the control plane knows I exist without manual Compose wiring.
11. As a storage node, I want to send periodic heartbeats with disk free-space metrics, so that the API can track my health and capacity.
12. As the API, I want to mark storage nodes unhealthy when heartbeats stop, so that new assignments and git operations fail fast instead of hanging.
13. As the API, I want to assign each new repository to the healthy node with the most free disk space, so that load is spread across the fleet.
14. As the API, I want repository-to-storage-node assignment to be sticky for the lifetime of the repository, so that routing stays predictable.
15. As a storage node, I want to accept git protocol commands only from dispatchers via a dedicated SSH key, so that user keys never need to exist on storage.
16. As a storage node, I want to run `git-upload-pack` and `git-receive-pack` without calling back to the API, so that git operations stay fast and storage stays simple.
17. As a storage node, I want an internal HTTP API protected by a per-node token, so that provisioning and deletion requests are authenticated beyond network trust alone.
18. As the API, I want to call the storage internal HTTP API to create a bare repo synchronously during repository creation, so that clients never see a metadata-only repository.
19. As the API, I want repository creation to fail if no healthy storage node is available, so that users get a clear error instead of a broken remote.
20. As the API, I want to call the storage internal HTTP API to delete a bare repo synchronously during repository deletion, so that disk and database stay consistent.
21. As a dispatcher, I want to pipe the client's stdin and stdout through an SSH session to the target storage node, so that the git wire protocol is forwarded transparently.
22. As a git client, I want denied access to fail with a useful error before any data transfer, so that unauthorized access is obvious.
23. As a git client, I want git operations to fail clearly when my repository's assigned storage node is down, so that I know the problem is infrastructure rather than permissions.
24. As a reader with repository access, I want read operations allowed when my role permits read access, so that private repository membership is enforced.
25. As a writer with repository access, I want push rejected when my role is read-only, so that write access is enforced at the dispatcher before proxying.
26. As an operator, I want storage nodes on the Docker internal network without public SSH exposure in production-like setups, so that users cannot bypass dispatcher authorization.
27. As an operator, I want dispatchers to remain stateless horizontally scalable entry points, so that adding dispatchers does not require per-repo configuration.
28. As an operator, I want new storage nodes to appear in the fleet by starting a container that self-registers, so that scaling storage does not require editing application code.
29. As the system, I want each storage node's repos volume disk usage reflected in heartbeat metrics, so that least-loaded assignment uses real free space on `/srv/git`.
30. As a security reviewer, I want user SSH public keys validated only at the dispatcher, so that the trust boundary between edge and storage is explicit.
31. As a developer running locally, I want `docker compose up` to continue working with a small default number of storage nodes, so that local development does not require extra tooling.
32. As a tester, I want an integration test that exercises push and clone through the dispatcher into a registered storage node, so that regressions in the full path are caught automatically.

## Implementation Decisions

### Major modules

The work splits into six deep modules with narrow interfaces. Each encapsulates substantial behavior behind a surface that should change rarely.

#### 1. Storage Node Registry (new backend feature)

Owns the `StorageNode` persistence model, registration, heartbeat ingestion, and health state.

**Responsibilities:**
- Accept registration from a storage node (node id, internal hostname, internal SSH port, internal HTTP port, per-node API token hash).
- Accept heartbeats carrying free bytes available on the repos volume (and optionally total bytes for observability).
- Mark nodes healthy on successful heartbeat; mark unhealthy after a configurable missed-heartbeat threshold.
- Expose queries for listing healthy nodes and fetching a node by id.

**Schema (conceptual):**

```
StorageNode {
  Id: Guid
  NodeId: string          // stable identifier, e.g. hostname or configured id
  InternalHost: string    // docker DNS name
  InternalSshPort: int    // default 22
  InternalHttpPort: int
  ApiTokenHash: string    // hashed per-node token
  FreeBytesAvailable: long
  TotalBytesAvailable: long   // optional
  LastHeartbeatAt: DateTimeOffset
  IsHealthy: bool
  RegisteredAt: DateTimeOffset
}
```

**Repository extension:**

```
Repository {
  ...existing fields...
  StorageNodeId: Guid?    // FK to StorageNode, required after this feature ships
  PhysicalPath: string    // canonical: /srv/git/{RepositoryId}.git
}
```

`PhysicalPath` becomes the single source of truth for on-disk location. It is set at creation to `/srv/git/{repositoryId}.git` and never derived from owner slug at runtime.

#### 2. Storage Node Selection (deep module, API-side)

**Interface:** given the set of healthy storage nodes, return the node id with the maximum `FreeBytesAvailable`, or none if the fleet is empty.

Pure function over registry state; unit-testable in isolation. Used only at repository creation time. Assignment is sticky — repositories do not move between nodes in this version.

#### 3. Storage Provisioner Client (deep module, API-side)

HTTP client that talks to a storage node's internal API using that node's per-node bearer token.

**Operations:**
- `ProvisionRepository(node, physicalPath)` → creates bare repo at path via `git init --bare`
- `DeleteRepository(node, physicalPath)` → removes bare repo directory

Returns structured success/failure so repository create/delete handlers can fail the HTTP request to the user when provisioning fails.

Orchestration order for create: pick node → generate repository id and physical path → call provision → persist repository row with `StorageNodeId` and `PhysicalPath`.

Orchestration order for delete: resolve node from repository → call delete on storage → remove DB row (or soft-delete, matching existing product conventions).

#### 4. Repository Access Check Routing (extend existing access-check module)

When access is allowed, the response must additionally include:

```
{
  allowed: true,
  repositoryId: Guid,
  physicalPath: "/srv/git/{id}.git",
  storageNode: {
    internalHost: string,
    internalSshPort: int
  },
  ...existing fields...
}
```

When access is allowed but the assigned storage node is unhealthy, return `allowed: false` with reason indicating storage unavailability (fail fast).

Dispatcher consumes this response; no second API round-trip for routing.

#### 5. Git Session Proxy (dispatcher module)

**Interface:** `ProxyGitSessionAsync(accessCheckResult, sshOriginalCommand, cancellationToken)`

After a successful access check:
1. Open an SSH session from dispatcher to `storageNode.internalHost:internalSshPort` using the dispatcher's internal private key (not the user's key).
2. Execute the parsed git command (`git-upload-pack` or `git-receive-pack`) with the **canonical physical path** from the access-check response (not the `{owner}/{slug}` path from the client).
3. Connect process stdin/stdout/stderr between the SSH session and the dispatcher process so the git client sees a normal git remote session.
4. Propagate the remote exit code.

Storage does not receive user public keys. The dispatcher is the only SSH client identity trusted on storage.

#### 6. Storage Node Runtime (repo-storage-layer extensions)

Two concerns in the storage container:

**A. Internal HTTP server (private network only)**
- `POST /internal/repos` — body: `{ "physicalPath": "/srv/git/{id}.git" }` — runs `git init --bare`, idempotent or conflict-safe
- `DELETE /internal/repos` — body or path param for physical path — removes bare repo
- `POST /internal/heartbeat` — optional if heartbeat is initiated by storage agent toward API instead; see below
- All endpoints require `Authorization: Bearer {per-node-token}`

**B. Storage agent (startup + background loop)**
- On boot: register with API (or re-register if already known), receive/store per-node token if generated by API
- Periodic heartbeat to API reporting free disk on `/srv/git` mount
- Configure OpenSSH to accept only the dispatcher internal public key in `authorized_keys`

**Storage SSH behavior:**
- `git` user with `git-shell` (or equivalent restricted command handling)
- No `AuthorizedKeysCommand` for user keys
- No API calls during git operations

### Architectural decisions (confirmed)

| Decision | Choice |
|----------|--------|
| Git execution location | Storage nodes; dispatcher proxies |
| Storage discovery | API registry + heartbeat |
| Trust model | Private network + dispatcher-only SSH key on storage |
| Repo → node assignment | Sticky at creation; pick healthy node with most free disk |
| On-disk path | `/srv/git/{repositoryId}.git`; DB `PhysicalPath` is canonical |
| Provisioning timing | Synchronous at API repository create |
| Deletion timing | Synchronous at API repository delete |
| Storage internal API auth | Per-node bearer token |
| Unhealthy assigned node | Fail fast on git ops; block create when no healthy nodes |
| v1 git operations | clone, fetch, pull, and push |
| Client URL shape | `{ownerSlug}/{repoSlug}` unchanged; slug resolution stays in API |
| Org-owned repos | Same URL pattern; existing owner-slug resolution applies |

### API contracts (new or extended)

**Storage node registration** (storage → API, on startup):
- Request: node id, internal host, ports, initial disk stats, optional registration secret for first-time enrollment
- Response: assigned storage node record id, per-node API token (plaintext once), heartbeat interval

**Storage node heartbeat** (storage → API, periodic):
- Request: node id, free bytes, total bytes, timestamp
- Response: acknowledged / unhealthy instruction

**Repository access check** (dispatcher → API, extended response):
- Add `physicalPath`, `storageNodeInternalHost`, `storageNodeInternalSshPort` on allowed responses
- Deny with storage-unavailable reason when node unhealthy

**Storage internal HTTP** (API → storage):
- Provision and delete as described above; authenticated with per-node token

### Docker & networking

- Storage services communicate on the Compose internal network only; remove host port mappings for storage in the default production-like compose profile (optional dev-only port mappings may remain for debugging).
- Dispatchers receive the internal SSH private key via environment or mounted secret; storage nodes receive the matching public key at image build or entrypoint.
- Default local development continues with a small fixed number of storage nodes (e.g. two); parametric compose generation is a follow-up (see Out of Scope).

### Dispatcher configuration

Extend dispatcher options with:
- Storage SSH private key path or PEM content
- Storage SSH user (`git`)
- Optional connection timeout for storage proxy

`DispatcherId` remains for logging; routing does not key off dispatcher identity.

## Testing Decisions

### What makes a good test here

Test observable behavior at module boundaries, not internal implementation details:
- Given registry state, selection picks the correct node
- Given an unhealthy assigned node, access check denies with the right reason
- Given a successful access check, dispatcher proxy invokes storage with the canonical physical path
- Given repository create, a bare repo exists at the expected path before the API returns success
- Given repository delete, the bare repo is gone after the API returns success

Prefer integration tests for the full SSH git path; unit tests for selection, parsing, and HTTP client error handling.

### Modules to test

| Module | Test type | Prior art |
|--------|-----------|-----------|
| Storage Node Selection | Unit tests in feature test project | Existing query handler tests (e.g. repository query handlers) |
| Storage Node Registry handlers | Unit tests with in-memory or test DB | `OpenGitBase.Features.*.Tests` query handler patterns |
| Storage Provisioner Client | Unit tests with mocked HTTP | API controller tests using substitutes |
| Repository create/delete orchestration | Integration or handler tests | `CreateRepositoryQueryHandlerTests`, `RepositoryControllerTests` |
| Access check routing extension | API controller tests | `RepositoryAccessChecksControllerTests` |
| Git Session Proxy | Unit tests for command/path mapping; optional integration test | `GitCommandParser` tests; `repo-storage-layer/scripts/integration-test.sh` |
| Storage internal HTTP | Shell integration test in repo-storage-layer | Existing `integration-test.sh` push/clone pattern |
| End-to-end dispatcher → storage | Compose-based integration test (stretch goal) | repo-storage-layer integration test |

### Recommended test priority for v1

1. Storage node selection algorithm
2. Extended access-check response including unhealthy node handling
3. Repository create/delete provisioning orchestration (mocked storage HTTP in unit tests; real HTTP in storage integration test)
4. Storage layer integration test extended for internal HTTP provision/delete
5. Dispatcher proxy integration test (full `git push` / `git clone` through dispatcher port)

## Out of Scope

- Automatic failover or re-routing to a different storage node when the assigned node fails
- Repository migration between storage nodes
- Replication or geo-redundancy
- Hash-based or round-robin assignment (instead of most-free-disk)
- mTLS between API and storage
- External service discovery (Consul, Kubernetes DNS, etc.)
- Storage nodes re-evaluating user SSH keys or calling the access-check API during git operations
- Direct public SSH access to storage nodes in production-like deployments
- Parametric Docker Compose generator for N storage nodes (acceptable follow-up; v1 may keep a fixed small number of manually defined storage services)
- Updating `StorageBytesUsed` quota accounting from actual git pack sizes on push (existing quota hooks may remain as-is; live pack accounting is not part of this PRD)
- HTTPS git smart HTTP transport
- LFS, submodules, or other git extensions beyond standard upload-pack/receive-pack

## Further Notes

### Relationship to existing MVP

The dispatcher already parses `git-upload-pack` / `git-receive-pack`, validates SSH keys via the API fingerprint endpoint, and calls `POST /api/v1/access-checks/repositories`. This PRD replaces the "access allowed → exit 0" stub with proxy execution and adds the storage fleet behind it.

The repo-storage-layer integration test already proves push and clone over SSH against a bare repo at `/srv/git`. This PRD connects that capability to the product's repository model, multi-node routing, and dispatcher authorization layer.

### Parametric Compose (follow-up)

Docker Compose cannot loop over service definitions in plain YAML. A small generator script (`STORAGE_NODES=N`) is the recommended approach when dynamic fleet sizing is needed. It is intentionally deferred so git proxy work is not blocked.

### Suggested implementation order

1. Storage Node registry feature (entity, registration, heartbeat, health)
2. Storage internal HTTP + agent in repo-storage-layer
3. Repository schema migration (`StorageNodeId`, updated `PhysicalPath` convention)
4. Repository create/delete orchestration with provisioner client
5. Extend access-check response with routing fields and unhealthy-node handling
6. Dispatcher git session proxy + storage SSH key wiring
7. Docker networking hardening (storage off public ports)
8. Integration tests for full path

### Open questions for implementation (not blocking PRD)

- Whether the API generates per-node tokens on registration or storage generates and presents them once — either works; API-generated is simpler for rotation later.
- Whether repository delete in the product is hard delete or soft delete today — deletion orchestration should match existing repository delete semantics.
- Exact missed-heartbeat threshold and heartbeat interval (suggest 30s heartbeat, 90s unhealthy threshold as starting defaults).
