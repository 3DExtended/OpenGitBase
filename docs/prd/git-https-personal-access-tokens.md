# PRD: Git HTTPS via Personal Access Tokens

## Problem Statement

OpenGitBase authenticates git clients over SSH at the dispatcher edge and proxies authorized sessions to storage nodes. This works in local development when port `2211` is exposed, but it does not work with Cloudflare Tunnels: the current tunnel terminates HTTP to the API load balancer only, and SSH-over-TCP through the tunnel is not a viable transport for end users.

Operators and developers need a git remote that works through Cloudflare HTTPS without exposing home IPs or opening inbound ports. Users need a self-service way to create credentials for `git clone`, `git fetch`, `git pull`, and `git push` — analogous to the existing SSH public key workflow, but compatible with HTTPS.

The product website is served at `www.opengitbase.com`, but git clone URLs should be shown and canonicalized on the apex domain (`opengitbase.com`) without the `www` prefix.

## Solution

Add Git Smart HTTP as the primary public git transport. Users create Personal Access Tokens (PATs) in the web UI and authenticate git HTTPS commands via HTTP Basic auth (password = token). The dispatcher remains the git edge: it validates tokens against the API, enforces repository permissions using the existing access-check logic, and reverse-proxies Smart HTTP requests to `git-http-backend` on the assigned storage node.

SSH authentication and proxying are **disabled by default** (not removed) behind a feature flag and Docker Compose profile so they can be re-enabled when Cloudflare Tunnels are no longer required. HAProxy on the existing load balancer becomes the single HTTP entry point for external traffic: API paths, git Smart HTTP paths, and the web UI are routed by path ACLs on one hostname. Cloudflare Tunnel targets this unified frontend.

## User Stories

### Git transport and URLs

1. As a developer, I want to `git clone https://opengitbase.com/{owner}/{repo}.git`, so that I can use standard git workflows through Cloudflare HTTPS.
2. As a developer, I want `git pull` and `git fetch` to work against an existing HTTPS remote, so that I can stay up to date without SSH.
3. As a developer, I want `git push` to work over HTTPS when I have write access, so that I can publish commits through the tunnel.
4. As a developer, I want denied access to return `401 Unauthorized` before any pack data is transferred, so that unauthorized access is obvious.
5. As a git client, I want git operations to fail clearly when my repository's assigned storage node is down, so that I know the problem is infrastructure rather than permissions.
6. As a repository reader, I want read operations allowed when my token has read scope and my role permits read access, so that private repository membership is enforced.
7. As a repository writer, I want push rejected when my token is read-only or my role is read-only, so that write access is enforced at the dispatcher before proxying.
8. As a developer using organization-owned repositories, I want `https://opengitbase.com/{orgSlug}/{repoSlug}.git` to work, so that org repos behave like user-owned repos from git's perspective.
9. As a developer visiting the web UI on `www.opengitbase.com`, I want clone instructions to show `https://opengitbase.com/...` (apex, no `www`), so that remotes match the canonical git hostname.
10. As a developer who mistakenly uses `www.opengitbase.com` in a git URL, I want to be redirected to the apex hostname for git paths, so that copy-paste mistakes still work.

### Personal Access Tokens

11. As a signed-in user, I want to create a Personal Access Token with a descriptive name, so that I can identify which credential belongs to which machine or CI job.
12. As a signed-in user, I want to choose read or write scope when creating a token, so that I can follow least-privilege for automation that only needs fetch.
13. As a signed-in user, I want to set an optional expiration (default 90 days) or choose no expiration, so that I can balance security and convenience.
14. As a signed-in user, I want to see the raw token exactly once at creation time, so that I can store it securely before it is lost.
15. As a signed-in user, I want to list my active tokens (name, scopes, expiry, last used — if tracked), so that I can audit what credentials exist.
16. As a signed-in user, I want to revoke a token, so that compromised credentials stop working immediately.
17. As a git client, I want to authenticate with `git clone https://{username}@opengitbase.com/owner/repo.git` using the token as the password, so that I can use standard git HTTPS conventions.
18. As a security reviewer, I want tokens stored as hashes only in the database, so that a DB leak does not expose usable credentials.
19. As a security reviewer, I want token prefixes (`ogb_`) so that tokens are identifiable in logs and support tooling.

### Dispatcher and storage

20. As a dispatcher, I want to parse Smart HTTP paths (`info/refs`, `git-upload-pack`, `git-receive-pack`) and map them to repository operations, so that I can call the same permission model as SSH.
21. As a dispatcher, I want the access-check response to include storage HTTP routing fields, so that I can proxy without additional lookups.
22. As a dispatcher, I want to reverse-proxy authorized Smart HTTP requests to the correct storage node's internal git HTTP port, so that bare repos stay on storage volumes.
23. As a dispatcher, I want to run as a long-lived HTTP server alongside optional SSH, so that many concurrent clone/push sessions are supported.
24. As a storage node, I want to serve Smart HTTP via `git-http-backend` on an internal port, so that the official git wire protocol is used without reimplementation.
25. As a storage node, I want git HTTP to trust only the private network (dispatcher proxies), so that user tokens are never validated on storage.
26. As a storage node, I want the existing internal provisioning HTTP API to remain separate from git Smart HTTP, so that lifecycle and git protocol concerns stay isolated.

### Routing and infrastructure

27. As an operator, I want HAProxy to route `/api/*` to API backends, git paths to dispatcher HTTP backends, and everything else to the web UI, so that one hostname serves the full product through Cloudflare.
28. As an operator, I want the Cloudflare Tunnel to target the unified HAProxy HTTP frontend, so that external HTTPS reaches git, API, and web without separate tunnel endpoints.
29. As an operator, I want dispatchers to remain stateless horizontally scalable entry points, so that adding dispatchers does not require per-repo configuration.
30. As an operator, I want SSH git disabled by default via `GIT_SSH_ENABLED=false`, so that the default deployment matches Cloudflare constraints.
31. As an operator, I want a Docker Compose `ssh` profile to opt into SSH git locally, so that I can test the legacy path without exposing it in production-like defaults.
32. As an operator, I want all SSH code, API endpoints, and fleet SSH key machinery to remain in the codebase when SSH is disabled, so that re-enabling SSH after migrating off Cloudflare is a configuration change, not a rewrite.

### Web UI

33. As a repository visitor with access, I want the repository overview page to show an HTTPS clone URL, so that I know how to connect without SSH.
34. As a repository visitor, I want a link to the Personal Access Tokens settings page from the clone instructions, so that I know how to create credentials.
35. As a signed-in user on the settings page, I want SSH key management hidden (with an explanatory note) when SSH is disabled, so that I am not confused by a non-working transport.
36. As a signed-in user, I want PAT management UI consistent with the existing SSH keys page patterns, so that the product feels cohesive.

### API and configuration

37. As the frontend, I want the API to expose `gitBaseUrl` (e.g. `https://opengitbase.com`), so that clone URLs are correct regardless of whether the user browses via `www`.
38. As the frontend, I want to fall back to stripping `www.` from the browser origin when `gitBaseUrl` is unavailable, so that local development works without extra config.
39. As the API, I want to validate PATs and resolve them to a user id, so that repository access checks reuse existing membership logic.
40. As the API, I want the repository access-check endpoint to accept either an SSH public key (when SSH enabled) or a PAT, so that one permission pipeline serves both transports.

### Testing and operations

41. As a tester, I want an end-to-end integration test that creates a PAT, pushes via HTTPS through HAProxy, and clones via HTTPS, so that regressions in the full path are caught automatically.
42. As a tester, I want unit tests for PAT validation, scope enforcement, and Smart HTTP path parsing, so that edge cases are covered without full Compose for every case.
43. As a developer running locally, I want `docker compose up` to support HTTPS git against the HAProxy git path without requiring Cloudflare, so that I can develop without tunnel credentials.

## Implementation Decisions

### Major modules

The work splits into eight deep modules with narrow interfaces. Each encapsulates substantial behavior behind a surface that should change rarely.

#### 1. Git Access Token (new backend feature)

Owns PAT persistence, creation, listing, revocation, and validation.

**Responsibilities:**
- Create token: generate opaque `ogb_`-prefixed secret, persist hash only, return plaintext once.
- List tokens for authenticated user (metadata only — never the secret).
- Revoke token by id (owner-only).
- Validate token: lookup by hash, check not revoked and not expired, return user id and scopes.

**Schema (conceptual):**

```
GitAccessToken {
  Id: Guid
  OwnerUserId: Guid
  Name: string
  TokenHash: string
  Scopes: string[]          // "read" | "write" in v1
  ExpiresAt: DateTimeOffset? // null = no expiration
  CreatedAt: DateTimeOffset
  RevokedAt: DateTimeOffset?
  LastUsedAt: DateTimeOffset? // optional v1
}
```

**HTTP API (authenticated, JWT cookie/session):**
- `POST /git-access-token` — create (body: name, scopes, optional expiresAt)
- `GET /git-access-token` — list for current user
- `DELETE /git-access-token/{id}` — revoke

**Validation query (internal / dispatcher-facing):**
- Input: raw token string
- Output: user id + scopes, or none if invalid/expired/revoked

#### 2. Repository Access Check — PAT extension (extend existing module)

Extend the existing repository access-check pipeline to accept credentials from either transport.

**Request extension:**

```
RepositoryAccessCheckRequest {
  // existing: PublicKey, RepositoryPath, Operation
  AccessToken?: string      // PAT plaintext from Basic auth password
}
```

Exactly one of `PublicKey` or `AccessToken` must be provided. PAT path resolves user via Git Access Token validation, then runs the same slug resolution, membership, role, quota, and storage routing logic as SSH.

**Scope enforcement:**
- `read` scope (or `write` scope): allows `upload-pack` operations (clone, fetch, pull).
- `write` scope only: allows `receive-pack` operations (push).

**Response extension:**

```
RepositoryAccessCheckResponse {
  // existing fields...
  StorageNodeInternalHttpPort?: int   // git Smart HTTP port on storage node
}
```

When access is allowed but the assigned storage node is unhealthy, return `allowed: false` with a storage-unavailability reason (unchanged behavior).

#### 3. Git Public Configuration (new API surface)

Expose runtime git configuration to clients.

**Configuration option:**

```
GitOptions {
  PublicBaseUrl: string     // e.g. "https://opengitbase.com"
  SshEnabled: bool          // mirrors GIT_SSH_ENABLED
}
```

**Endpoint:**
- `GET /api/v1/git/config` (or equivalent) — returns `{ gitBaseUrl, sshEnabled }` for UI and tooling.

#### 4. Git Smart HTTP Edge (dispatcher module — new HTTP server)

Long-lived Kestrel HTTP server in the dispatcher container.

**Interface:** handle incoming Smart HTTP requests at `/{ownerSlug}/{repoSlug}.git/...`

**Per-request flow:**
1. Parse path → owner slug, repo slug, Smart HTTP operation (`info/refs` + service query, `git-upload-pack`, `git-receive-pack`).
2. Extract PAT from `Authorization: Basic` (password = token; username ignored or logged only).
3. Map HTTP operation to `RepositoryOperation` (upload-pack vs receive-pack).
4. Call `POST /api/v1/access-checks/repositories` with token + `{owner}/{repo}` path + operation.
5. If denied → `401 Unauthorized` (or `403` when authenticated but not permitted).
6. If allowed → reverse-proxy request body and headers to `http://{storageNodeInternalHost}:{storageNodeInternalHttpPort}{physicalPath-relative-url}`.
7. Stream response back unchanged (status, git content types, pack data).

**Path parsing rules:**
- Client URL shape: `/{ownerSlug}/{repoSlug}.git/info/refs`, `.../git-upload-pack`, `.../git-receive-pack`.
- Slug resolution remains in the API; dispatcher forwards using canonical `physicalPath` from access-check response.

**Container runtime:**
- Entrypoint starts Kestrel HTTP listener (always).
- Optionally starts `sshd` when `GIT_SSH_ENABLED=true`.

#### 5. Storage Git HTTP Backend (repo-storage-layer extension)

Second internal HTTP server alongside the existing provisioning API.

**Port:** internal `:8082` (configurable; distinct from provisioning `:8081`).

**Implementation:** `git-http-backend` invoked as CGI (via `fcgiwrap`, a minimal CGI wrapper, or equivalent) with:
- `GIT_PROJECT_ROOT=/srv/git`
- `PATH_INFO` mapping to the bare repo path
- Push enabled only when request originates from dispatcher (network trust); no per-user auth on storage.

**Storage node registry extension:**
- `InternalGitHttpPort` on `StorageNode` (default 8082), returned in access-check routing and heartbeats/registration as needed.

**Container runtime:**
- Start git HTTP backend process alongside existing `storage-http-server.py` and optional `sshd`.

#### 6. Unified HTTP Routing (HAProxy extension)

Single HTTP frontend on the load balancer container.

**ACL rules (order matters):**
1. Host `www.*` + path matches git Smart HTTP pattern → `301` redirect to apex hostname (preserve path and query).
2. Path prefix `/api` → API backends (existing).
3. Path matches `^/[^/]+/[^/]+\.git` (and subpaths) → dispatcher HTTP backends.
4. Default → web UI backends.

**Cloudflare Tunnel:**
- Target the unified HAProxy HTTP frontend (not API directly).

**Dispatcher HTTP backend:**
- Balance round-robin across `dispatcher-1` and `dispatcher-2` HTTP ports (new bind port, e.g. `:8082` on dispatcher containers, fronted by HAProxy).

#### 7. SSH Disable Gate (configuration module)

**Runtime flag:** `GIT_SSH_ENABLED` (default `false`).

**When false:**
- Dispatcher `sshd` does not start (or rejects connections immediately with a clear message).
- HAProxy SSH TCP frontend (`:22` / host `:2211`) excluded from default compose profile.
- Web UI hides SSH keys settings link/section; shows note that HTTPS tokens are the active method.
- API `git/config` returns `sshEnabled: false`.

**When true:**
- Existing SSH path unchanged: `AuthorizedKeysCommand` → fingerprint API → access check → SSH proxy to storage.

**Compose profile:** `ssh` — includes SSH load balancer frontend, dispatcher SSH ports, and related host port mappings. Default `docker compose up` omits this profile.

All SSH-related code (`PublicGitSshKey` feature, fleet dispatcher SSH keys, `ssh-auth-hook`, `GitSessionProxyService`, storage SSH `authorized_keys`) remains intact behind the gate.

#### 8. Web UI — Tokens and Clone URLs (frontend module)

**New settings page:** Personal Access Tokens (create with name, scope, expiry; list; revoke; one-time secret display on create).

**Repository overview:** show HTTPS clone URL using `gitBaseUrl` from API, format `https://opengitbase.com/{owner}/{repo}.git`.

**Settings navigation:** link to PAT page; conditionally hide SSH keys when `sshEnabled` is false.

**i18n:** new translation keys for PAT UI and HTTPS clone hints.

### Architectural decisions (confirmed)

| Decision | Choice |
|----------|--------|
| Public git transport (default) | HTTPS Smart HTTP via Cloudflare |
| Public URL shape | `https://opengitbase.com/{ownerSlug}/{repoSlug}.git` on apex domain |
| Web hostname | `www.opengitbase.com` — git URLs omit `www` |
| www git requests | `301` redirect to apex for git paths |
| Traffic splitting | HAProxy path ACLs on single hostname |
| Git protocol implementation | `git-http-backend` on storage nodes |
| Git edge authorization | Dispatcher validates PAT via API, then reverse-proxies |
| Credential model | Personal Access Tokens (`ogb_` prefix, hashed at rest) |
| Git client auth | HTTP Basic (password = token) |
| Token scopes (v1) | `read` (fetch/clone) and `write` (push) |
| Token expiration | Optional; default 90 days; allow no expiration |
| SSH transport | Disabled by default; code retained; re-enabled via flag + compose profile |
| Dispatcher shape | Same container/fleet: Kestrel HTTP + optional sshd |
| Slug resolution | API only (unchanged) |
| On-disk path | `/srv/git/{repositoryId}.git` (unchanged) |
| Storage trust model | Private network; storage does not validate user tokens |
| Delivery scope | End-to-end: backend, storage, dispatcher, HAProxy, tunnel, UI, tests |

### API contracts (new or extended)

**Git config (API → clients):**
- Response: `{ "gitBaseUrl": "https://opengitbase.com", "sshEnabled": false }`

**Git access token CRUD (JWT-authenticated user):**
- Create response includes `{ "id", "name", "scopes", "expiresAt", "token" }` where `token` is plaintext shown once.
- List response: array without `token` field.

**Repository access check (dispatcher → API, extended):**
- Request accepts `accessToken` OR `publicKey` (mutually exclusive).
- Allowed response adds `storageNodeInternalHttpPort`.

**Storage node registration/heartbeat (extended):**
- Include `internalGitHttpPort` in node metadata.

### Configuration

| Setting | Purpose | Example |
|---------|---------|---------|
| `Git__PublicBaseUrl` | Canonical clone URL host | `https://opengitbase.com` |
| `GIT_SSH_ENABLED` | Enable SSH git path | `false` (default) |
| `Dispatcher__HttpPort` | Dispatcher Kestrel listen port | `8082` |
| Storage `STORAGE_GIT_HTTP_PORT` | Internal git-http-backend port | `8082` |

### Suggested implementation order

1. Git Access Token backend feature (entity, migration, CRUD API, validation query)
2. Extend repository access-check for PAT + storage HTTP port in response
3. Git public config endpoint (`gitBaseUrl`, `sshEnabled`)
4. Storage git HTTP backend on internal port
5. Dispatcher Kestrel Smart HTTP handler + reverse proxy to storage
6. HAProxy unified HTTP frontend + www→apex git redirect
7. SSH disable gate (env var + compose profile)
8. Cloudflare tunnel repoint to unified HAProxy frontend
9. Web UI: PAT management + HTTPS clone URLs + conditional SSH UI
10. End-to-end integration test (PAT → push → clone over HTTPS)

## Testing Decisions

### What makes a good test here

Test observable behavior at module boundaries, not internal implementation details:
- Given a valid read-scoped PAT, access check allows upload-pack and denies receive-pack.
- Given a write-scoped PAT, access check allows both operations when role permits.
- Given an expired or revoked PAT, access check denies with authentication failure.
- Given a successful access check, dispatcher proxies to the correct storage node and physical path.
- Given a git path on `www`, HAProxy redirects to apex.
- Given `GIT_SSH_ENABLED=false`, SSH connections are rejected and UI hides SSH keys.
- Given repository create, HTTPS clone after PAT-authenticated push returns real objects.

Prefer unit tests for token validation, scope mapping, and path parsing; integration tests for storage git HTTP and full HTTPS git path through HAProxy.

### Modules to test

| Module | Test type | Prior art |
|--------|-----------|-----------|
| Git Access Token handlers | Unit tests in feature test project | `PublicGitSshKey` query handler tests |
| Git Access Token API controller | API controller tests | `PublicGitSshKeyControllerTests` |
| PAT access-check extension | API controller tests | `RepositoryAccessChecksControllerTests` |
| Smart HTTP path / operation parsing | Unit tests in dispatcher test project | `GitCommandParserTests` |
| Dispatcher HTTP reverse proxy | Unit tests with mocked HTTP handler; optional integration | `GitSessionProxyServiceTests` |
| Storage git HTTP backend | Shell integration test in repo-storage-layer | Existing `integration-test.sh` |
| HAProxy routing | Manual or script-based smoke test | Rolling update verification scripts |
| End-to-end HTTPS git | Compose-based shell test | `e2e-git-proxy-test.sh` (SSH variant) |
| Web PAT UI | Component or e2e tests | `ssh-keys.vue` patterns |

### Recommended test priority

1. PAT validation and scope enforcement (unit)
2. Extended access-check with PAT credentials (API tests)
3. Smart HTTP path parsing and operation mapping (dispatcher unit)
4. Storage git HTTP smoke test (provision repo → `git clone` over internal HTTP)
5. Full e2e: create PAT → `git push` → `git clone` through HAProxy HTTPS path

## Out of Scope

- OAuth device flow or third-party OAuth providers for git HTTPS (PAT + Basic auth only in v1)
- Fine-grained token scopes beyond `read` and `write` (e.g. per-repository tokens, admin scopes)
- Token rotation API (revoke + create new is sufficient in v1)
- Automatic PAT expiration reminders or email notifications
- Bearer-token-only git auth without Basic auth support
- LibGit2Sharp or custom Smart HTTP protocol implementation in .NET
- Removing SSH code, database tables, fleet SSH key management, or `PublicGitSshKey` feature
- Changing repository-to-storage-node assignment or failover behavior
- LFS, submodules, or git extensions beyond standard upload-pack/receive-pack
- Separate `git.` subdomain (path-based routing on apex domain is the chosen model)
- mTLS between dispatcher and storage
- Cloudflare dashboard configuration automation (document manual tunnel hostname setup only)

## Further Notes

### Relationship to Git Storage Proxy PRD

The [Git Storage Proxy PRD](./git-storage-proxy.md) delivered SSH-based git proxying, storage node registry, provisioning HTTP API, and access-check routing with `physicalPath` and storage SSH ports. That SSH path remains implemented and is gated off, not deleted.

That PRD explicitly listed "HTTPS git smart HTTP transport" as out of scope. **This PRD delivers that transport** and makes it the default public git entry point.

### Cloudflare and hostnames

- Web UI: `www.opengitbase.com`
- Canonical git clone URLs: `https://opengitbase.com/{owner}/{repo}.git`
- Both hostnames should reach HAProxy; git paths on `www` redirect to apex
- Tunnel token and hostname configuration in Cloudflare dashboard remain operator-managed; compose documents the internal origin URL

### Security notes

- PATs are equivalent to passwords for git access; treat creation UX like API key reveal (show once, copy prompt).
- Rate-limit access-check and git HTTP endpoints consistent with existing sensitive endpoint limits.
- Do not log raw tokens; log token id or prefix only.
- Storage git HTTP must not be exposed on host ports in production-like compose profiles.

### SSH re-enablement path

When Cloudflare Tunnels are no longer required:
1. Set `GIT_SSH_ENABLED=true`
2. Start compose with `--profile ssh`
3. Re-expose HAProxy SSH frontend
4. Show SSH keys UI again via `sshEnabled: true` in git config
5. Users may use either HTTPS PATs or SSH keys

No code archaeology should be required.
