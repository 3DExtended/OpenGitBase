<!-- forge: #26 -->

# PRD: Zero-Downtime Rolling Docker Compose Updates

## Problem Statement

Developers running the local OpenGitBase Docker stack currently refresh code by tearing down the entire stack (`docker compose down`) and rebuilding everything (`docker compose up -d --build`). This cold-start workflow takes on the order of five minutes and interrupts all traffic — Git over SSH, the web UI, the API, and external access via the Cloudflare tunnel.

The stack already has horizontally scaled fleet components (two storage nodes, two dispatchers behind an SSH HAProxy frontend), but the API and web UI are single-instance services exposed directly on host ports. There is no migrate-first deploy path, so rolling API updates risk schema races when EF migrations are pending. After a typical `git pull`, developers change the API, web UI, and fleet layers together and need a fast, reliable way to update without downtime.

## Solution

Extend the existing HAProxy load balancer to front HTTP traffic for replicated API and web services, add a one-shot database migration job that runs before any API instance is recreated, and provide a single orchestration script (`./scripts/rolling-update.sh`) that rebuilds images and recreates containers one at a time in dependency order while waiting for health checks at each step.

Git SSH traffic continues through the existing TCP frontend with round-robin across dispatchers. API and web traffic flows through new HTTP frontends on the same HAProxy container. The Cloudflare tunnel targets the API HTTP frontend so external access survives API instance rolls. Developers run one command after `git pull` instead of `docker compose down && … up`.

## User Stories

1. As a developer, I want to update the local stack after `git pull` without running `docker compose down`, so that I avoid a five-minute cold start.
2. As a developer, I want a single command to rebuild and roll all changed services, so that I do not have to remember per-service compose invocations.
3. As a developer, I want Git over SSH to remain available during stack updates, so that long-running clone or push operations are not cut off by dispatcher recreation.
4. As a developer, I want the web UI to remain reachable during updates, so that I can keep working in the browser while containers roll.
5. As a developer, I want the API to remain reachable during updates, so that in-flight API requests and health checks do not all fail at once.
6. As a developer using the Cloudflare tunnel, I want external API access to survive rolling updates, so that remote clients and tunnel-backed workflows keep working.
7. As a developer, I want pending EF migrations applied once before any API container is recreated, so that schema changes do not race across multiple API startups.
8. As a developer, I want the rolling update to fail fast when migration or health checks fail, so that broken deploys leave the previous containers running.
9. As a developer, I want clear recovery instructions when a rolling update aborts, so that I can fix the problem without wiping volumes.
10. As a developer, I want storage node containers updated one at a time, so that at least one storage node stays healthy while the other restarts.
11. As a developer, I want dispatcher containers updated one at a time behind HAProxy, so that Git SSH entrypoint traffic is load-balanced across a healthy peer during each roll.
12. As a developer, I want API containers updated one at a time behind the HTTP load balancer, so that API traffic always has a healthy backend during image swaps.
13. As a developer, I want web containers updated one at a time behind the HTTP load balancer, so that the SPA and `/api` proxy path stay available during web image swaps.
14. As a developer, I want each web replica to proxy `/api` to the load-balanced API backends, so that browser traffic does not depend on a single API hostname.
15. As a developer, I want HAProxy health checks on API and web backends, so that unhealthy instances are removed from rotation before traffic hits them.
16. As a developer, I want `localhost:3000` to remain the web entrypoint after replication, so that existing bookmarks and docs stay valid.
17. As a developer, I want `localhost:8089` (or equivalent) to remain the direct API entrypoint for tools like `curl` and `bootstrap-fleet.sh`, so that local scripts continue to work.
18. As a developer, I want `docker-compose.override.yml` to continue supporting API secrets and web build args, so that environment customization is not lost when services are replicated.
19. As a developer, I want the first-time bootstrap flow (`postgres` → `api` → `bootstrap-fleet.sh` → full stack) to keep working, so that new contributors are not blocked by the replication changes.
20. As a developer, I want rolling updates to assume backward-compatible migrations, so that old API binaries keep working against the new schema while instances roll.
21. As a developer making a breaking schema change, I want the rolling script to stop with an explicit error, so that I know to fall back to a maintenance workflow rather than corrupting state.
22. As a developer, I want the rolling script to rebuild only what changed where practical, so that update time stays well under the current five-minute full teardown.
23. As a developer, I want the rolling script to verify API health through the load balancer after API rolls, so that I get confidence the edge path is correct.
24. As a developer, I want the rolling script to verify tunnel reachability when the tunnel service is enabled, so that external access is confirmed before the script exits successfully.
25. As a developer, I want storage and dispatcher services to depend on a stable API hostname (load balancer), so that fleet enrollment and heartbeats are not tied to a single API container name.
26. As a developer, I want admin user seeding to remain idempotent across two API instances, so that both replicas can start without conflicting seed writes.
27. As a developer, I want README and override example documentation updated, so that the rolling workflow is discoverable without reading compose internals.
28. As an operator, I want the SSH HAProxy frontend unchanged in behavior, so that Git entrypoint on port 2211 continues to balance across dispatchers with TCP health checks.
29. As a tester, I want the migrate-only API mode testable without starting the full web host, so that migration logic can be verified in isolation.
30. As a tester, I want HAProxy configuration validated before containers roll, so that a syntax error does not take down the entire edge.

## Implementation Decisions

### Major modules

The work splits into six deep modules with narrow interfaces. Each encapsulates substantial behavior behind a surface that should change rarely.

#### 1. HAProxy HTTP edge extension (extend existing `ssh-lb`)

Owns all north-south load balancing for the local stack: existing Git SSH TCP frontend plus new HTTP frontends for API and web.

**Responsibilities:**
- Keep the existing TCP frontend on container port 22 (host-mapped to 2211) balancing `dispatcher-1` and `dispatcher-2` with TCP health checks.
- Add an HTTP frontend for the API on host port 8089 (or equivalent), balancing `api-1` and `api-2` with HTTP health checks against `/health`.
- Add an HTTP frontend for the web UI on host port 3000, balancing `web-1` and `web-2` with HTTP health checks against `/`.
- Drain backends during recreation by relying on health-check failure before container stop.

**Interface:** static configuration consumed by the HAProxy container; validated with `haproxy -c` before reload or dependent service rolls.

#### 2. Replicated compose topology (API and web)

Replaces single `api` and `web` services with paired replicas and removes direct host port bindings from individual replicas.

**Responsibilities:**
- Define `api-1` and `api-2` with identical configuration (shared environment, build context, dependencies on Postgres).
- Define `web-1` and `web-2` with identical build args and runtime config.
- Expose API and web only through HAProxy HTTP frontends on the host.
- Update fleet services (`storage-1`, `storage-2`, `dispatcher-1`, `dispatcher-2`) to depend on API availability via the load-balanced API hostname rather than a single `api` container DNS name.
- Update Cloudflare tunnel service to target the HAProxy API HTTP frontend (internal URL) instead of a single API container.

**Compose pattern:** use YAML anchors or extension fields so `docker-compose.override.yml` environment blocks apply once to both API replicas and once to both web replicas.

#### 3. API migrate-only mode (application entrypoint)

A dedicated execution path on the existing API image that applies pending EF migrations and exits without starting Kestrel or long-running hosted services.

**Responsibilities:**
- Detect migrate-only invocation (environment variable or CLI flag — choose one stable mechanism).
- Run the same `Database.MigrateAsync()` path used at startup today.
- Skip `AdminUserSeedService` and HTTP server startup in migrate-only mode.
- Exit non-zero on migration failure so the rolling script aborts before touching API replicas.

**Interface:**

```
RunMigrationsAsync() → success | failure
```

Invoked by a one-shot `api-migrate` compose service using the API image with overridden command/entrypoint.

#### 4. Web upstream rewire (Caddy in web image)

The Nuxt SPA container already serves static assets and reverse-proxies `/api` to the backend. With replicated APIs, the proxy target must be the stable load-balanced API hostname (HAProxy internal service name on the Docker network), not a single `api` container.

**Responsibilities:**
- Point Caddy `reverse_proxy` at the HAProxy API backend (or a dedicated internal DNS alias that resolves to it).
- Preserve `uri strip_prefix /api` behavior so API routes remain unchanged.

#### 5. Rolling update orchestrator (`scripts/rolling-update.sh`)

Single developer-facing command that encodes the full zero-downtime sequence.

**Responsibilities:**
1. Accept compose file arguments matching the project convention (`docker-compose.yml` + `docker-compose.override.yml`).
2. Build images for services that may have changed (at minimum API, web, storage, dispatcher; optionally all built services).
3. Run `api-migrate` one-shot and wait for successful exit.
4. Roll fleet in order: `storage-1` → wait healthy → `storage-2` → wait healthy → `dispatcher-1` → wait healthy → `dispatcher-2` → wait healthy.
5. Roll API: `api-2` → wait healthy via LB → `api-1` → wait healthy via LB.
6. Roll web: `web-2` → wait healthy via LB → `web-1` → wait healthy via LB.
7. Verify final health: API `/health` through host LB port, web `/` through host LB port, optional tunnel smoke check when tunnel service is running.
8. On any failure: print which step failed and manual recovery guidance; do not run `docker compose down`.

**Interface:**

```
rolling-update.sh [--compose-files …] [--skip-tunnel-check]
  → exit 0 on success, non-zero on abort
```

Uses `docker compose up -d --build --no-deps <service>` per step and polls health endpoints with timeouts.

#### 6. Documentation and override template alignment

Keep bootstrap and daily workflows coherent after topology changes.

**Responsibilities:**
- Update README local development section: replace `down && up` recommendation with `rolling-update.sh` for code refreshes; retain `down -v` for wipe-and-reseed.
- Update `docker-compose.override.example.yml` comments and structure for replicated services.
- Update `bootstrap-fleet.sh` API URL if the health entrypoint port or path changes (should remain `localhost:8089/health` via HAProxy).
- Document first-time bootstrap differences if initial `postgres + api` step becomes `postgres + api-1 + api-2 + haproxy` or a documented minimal subset.

### Architectural decisions (confirmed)

| Decision | Choice |
|----------|--------|
| Downtime goal | True zero-downtime for SSH, web, and API during normal dev updates |
| Services typically changed | API, web, storage nodes, dispatchers |
| HTTP load balancing | Extend existing HAProxy container (SSH TCP + new HTTP frontends) |
| API replication | `api-1` / `api-2` behind HAProxy API frontend |
| Web replication | `web-1` / `web-2` behind HAProxy web frontend |
| Migration strategy | One-shot `api-migrate` service before rolling API replicas |
| Migration compatibility | Assume backward-compatible; fail fast with recovery steps on error |
| Cloudflare tunnel | Used in local dev; tunnel targets HAProxy API frontend |
| Daily workflow | `./scripts/rolling-update.sh` after `git pull` |
| Avoid | `docker compose down` for routine code updates |

### Ordering and health semantics

| Phase | Action | Health gate |
|-------|--------|-------------|
| 0 | Build images | — |
| 1 | `api-migrate` one-shot | exit code 0 |
| 2 | Roll `storage-1` | storage healthcheck |
| 3 | Roll `storage-2` | storage healthcheck |
| 4 | Roll `dispatcher-1` | dispatcher healthcheck + SSH LB backend up |
| 5 | Roll `dispatcher-2` | dispatcher healthcheck |
| 6 | Roll `api-2` | `GET /health` via HAProxy → 200 |
| 7 | Roll `api-1` | `GET /health` via HAProxy → 200 |
| 8 | Roll `web-2` | `GET /` via HAProxy → 200 |
| 9 | Roll `web-1` | `GET /` via HAProxy → 200 |
| 10 | Final verification | API + web LB + optional tunnel |

Roll the `-2` instance before `-1` so the conventionally "primary" `-1` container is refreshed last while `-2` carries traffic during the final swap.

### API healthcheck

Add a compose healthcheck on `api-1` and `api-2` using `GET /health` (same endpoint used by `bootstrap-fleet.sh` and HAProxy). HAProxy HTTP checks and compose healthchecks should align on the same path and expected status codes.

## Testing Decisions

### What makes a good test here

Test observable behavior at boundaries, not script internals:
- Migrate-only mode applies pending migrations and exits without listening on HTTP.
- Migrate-only mode exits non-zero when migration fails.
- HAProxy configuration passes syntax validation.
- Rolling script aborts before recreating API replicas when migrate step fails (dry-run or mocked compose).
- After a successful roll, `/health` and web root respond through load balancer ports.

Prefer focused unit/integration tests for migrate-only mode; shell-level smoke tests or `haproxy -c` for infrastructure config; manual or CI compose smoke for the full rolling path.

### Modules to test

| Module | Test type | Prior art |
|--------|-----------|-----------|
| API migrate-only mode | Unit or integration test invoking migrate flag/env | `DependencyInjectionHelpers` migration path; API test project patterns |
| HAProxy config | Config validation (`haproxy -c`) in script self-check or CI step | `docker/haproxy/haproxy.cfg` consumed at runtime |
| Rolling update script | Shell smoke with `--dry-run` or documented manual test plan | `scripts/bootstrap-fleet.sh`, `applications/repo-storage-layer/scripts/e2e-git-proxy-test.sh` |
| Web Caddy upstream | Manual or e2e: `/api` requests succeed with one API backend down | Existing web Docker + Caddy setup |

### Recommended test priority for v1

1. API migrate-only mode — success and failure exit codes (highest value, smallest surface).
2. HAProxy config syntax validation in rolling script or CI.
3. Documented manual test plan for full rolling update (git pull → script → verify SSH, web, tunnel).
4. Optional: rolling script dry-run mode that prints planned steps without executing (aids review, not required for v1).

Admin user seed idempotency already exists; no new seed tests required unless replication exposes a regression.

## Out of Scope

- Production Kubernetes or swarm deployment patterns
- Blue/green or canary deploys beyond pairwise rolling in Compose
- Automatic handling of breaking (non-backward-compatible) EF migrations
- Parametric scaling beyond two API/web replicas
- Replacing HAProxy with Traefik, nginx, or Caddy as the edge LB
- Zero-downtime Postgres upgrades or major version migrations
- CI pipeline integration for rolling updates (local dev only in v1)
- `docker compose watch` or hot-reload without image rebuild
- Image registry push/pull; all builds remain local
- Load testing or formal SLO measurement for update duration
- Renaming `ssh-lb` container (optional cosmetic change; not required)

## Further Notes

### Relationship to existing stack

The git storage proxy PRD established the two-storage-node, two-dispatcher fleet behind HAProxy for SSH. This PRD completes the same zero-downtime story for the control plane (API) and web UI surfaces that developers hit on every code change.

`Database.MigrateAsync()` already runs on API startup in `DependencyInjectionHelpers`. Migrate-only mode extracts that path so rolling updates do not depend on which API instance happens to start first.

### Breaking migrations

When a migration removes or renames columns in a way the old API binary cannot tolerate, true zero-downtime is impossible. The rolling script should fail fast during migrate or health-check phases and document fallback: wipe dev database (`docker compose down -v`) or accept brief outage with a manual `down && up`.

### Suggested implementation order

1. HAProxy HTTP frontends + health checks (config only, can validate with `haproxy -c`)
2. Compose replication (`api-1`/`api-2`, `web-1`/`web-2`, tunnel and fleet dependency updates)
3. API migrate-only mode + `api-migrate` compose service
4. Web Caddy upstream rewire
5. `scripts/rolling-update.sh`
6. README and override example updates
7. Manual end-to-end verification (SSH git, web UI, tunnel, migrate deploy)

### Open questions for implementation (not blocking PRD)

- Exact migrate-only trigger: CLI flag vs environment variable (prefer env var for compose `environment:` override simplicity).
- Whether first-time bootstrap should start a single API instance before replication is enabled, or start the full replicated stack immediately after documenting the longer initial path.
- Tunnel verification method: internal `docker exec` curl vs external URL check (depends on tunnel token configuration in override file).
