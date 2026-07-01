# OpenGitBase

Self-hosted Git platform (early stage).

Manual merge-request smoke fixtures live under [`test/fixtures/mr-smoke/`](test/fixtures/mr-smoke/).

## Licensing

OpenGitBase is **source-available**, not OSI-approved open source. Source code
is public, but production use is subject to conditions.

### Free production use

You may run Opengitbase in production **without a commercial license** when:

- Use is **internal** to your organization (no third-party hosting), **and**
  your organization has **ARR below $1M** and **300 or fewer employees**, **or**
- Use is solely for an **OSI-licensed open-source project** (any sponsor size).

Development and testing use is unrestricted.

### Commercial license required

You need a [commercial license](COMMERCIAL-LICENSE.md) for production use when:

- Your organization has **$1M+ ARR** or **more than 300 employees**, or
- You provide **third-party access** (managed hosting, SaaS, resale, etc.) —
  **any organization size**.

### Modifications

You may fork and modify the code. Sharing modifications is **optional** — there
is no copyleft requirement for qualifying free-tier users.

### Documentation

| Document | Description |
|----------|-------------|
| [LICENSE](LICENSE) | Full license terms |
| [COMMERCIAL-LICENSE.md](COMMERCIAL-LICENSE.md) | Paid production use |
| [TRADEMARK.md](TRADEMARK.md) | Brand and fork naming rules |
| [docs/licensing.md](docs/licensing.md) | FAQ with worked examples |
| [CONTRIBUTING.md](CONTRIBUTING.md) | How to contribute (DCO required) |

Compliance is self-reported (honor system) in v1 — no license keys in the software.

## Local development (Docker)

The default stack runs the API, web UI, Postgres, **three storage nodes**, and two
git dispatchers. Storage nodes are enrolled by an admin user and identified by
machine certificates — fleet SSH keys are not checked into the repo.

Repository replication (RF=3) requires all three storage nodes to be healthy before
new repositories can be created.

### Prerequisites

- Docker and Docker Compose
- `curl`, `openssl`, and Python 3 (for `scripts/bootstrap-fleet.sh`)

### First-time startup

```bash
# 1. Start database, API replicas, and HAProxy
docker compose up -d --build postgres api-1 api-2 ssh-lb

# Wait until the API is healthy (via HAProxy)
curl -fsS http://localhost:8089/health

# 2. Copy override template and generate PKI, admin enrollments, and fleet SSH keys
cp docker-compose.override.example.yml docker-compose.override.yml
# Edit REPLACE_WITH_CLOUDFLARE_TUNNEL_TOKEN in docker-compose.override.yml if using the tunnel
./scripts/bootstrap-fleet.sh
# writes fleet tokens into docker-compose.override.yml (do not commit)

# 3. Start the full stack
docker compose -f docker-compose.yml -f docker-compose.override.yml up -d --build
```

### After bootstrap

For daily code updates after the initial bootstrap, use the rolling update script
instead of tearing down and recreating the whole stack:

```bash
./scripts/rolling-update.sh
```

Requires `docker-compose.override.yml` (created during bootstrap). The script
rebuilds images and recreates containers one at a time while waiting for health
checks. By default only API and web are rolled; pass `--full` when storage or
dispatcher code changed.

### Wipe and reseed

To reset the database and start fresh:

```bash
docker compose -f docker-compose.yml -f docker-compose.override.yml down -v
```

Then repeat the first-time startup steps.

| Service | URL |
|---------|-----|
| Unified HTTP (web, API, Git) | <http://localhost:8089> |
| Web UI (alias) | <http://localhost:3000> |
| Git over HTTPS (via HAProxy) | `http://localhost:8089/{owner}/{repo}.git` |
| Git over HTTPS (dispatcher direct, debug) | <http://localhost:8822> |
| SSH git (optional `--profile ssh`) | `ssh://git@localhost:2211/owner/repo` |

### Git over HTTPS (local)

1. Create a [personal access token](http://localhost:8089/settings/access-tokens) in the web UI.
2. Clone with the token as the password:

```bash
git clone http://git:YOUR_TOKEN@localhost:8089/OWNER/REPO.git
```

Run the unified E2E regression suite against a running stack:

```bash
dotnet run --project tests/OpenGitBase.E2E.Runner
# Or with compose already up:
dotnet run --project tests/OpenGitBase.E2E.Runner -- --skip-compose
```

See [tests/OpenGitBase.E2E/README.md](tests/OpenGitBase.E2E/README.md) for profiles, baselines, and flags. Requires a healthy Compose API (`curl -fsS http://localhost:8089/health`).

### SSH git (optional)

SSH is disabled by default. To enable the legacy SSH path:

```bash
docker compose -f docker-compose.yml -f docker-compose.ssh.yml --profile ssh up -d
```

Set `GIT_SSH_ENABLED=true` on API and dispatchers via `docker-compose.ssh.yml`.

### Cloudflare tunnel

The tunnel container targets the unified HAProxy frontend (`ssh-lb:8080`). In the Cloudflare dashboard, route `opengitbase.com` (and optionally `www.opengitbase.com`) to the tunnel. Git Smart HTTP paths on `www` are redirected to the apex hostname by HAProxy.

After rolling web updates, **purge the Cloudflare cache** for the zone (Caching → Configuration → Purge Everything). During deploys, missing `/_nuxt/*` chunks can briefly be cached as HTML; a purge clears poisoned asset entries.

Default admin user (change in `applications/OpenGitBase.Api/appsettings.json`):

- Username: `admin`
- Password: `change-me-admin`

Verify storage nodes registered (must run from inside the Docker network — the
host gets 403 by design):

```bash
docker exec opengitbase_storage_1 \
  curl -fsS http://api-lb:8080/api/v1/storage-nodes/healthy
```

### Re-bootstrap

Re-run `./scripts/bootstrap-fleet.sh` when you need new enrollment tokens or a
new fleet bootstrap token (for example after wiping the database). Then recreate
storage and dispatcher containers:

```bash
docker compose -f docker-compose.yml -f docker-compose.override.yml up -d --force-recreate storage-1 storage-2 storage-3 dispatcher-1 dispatcher-2
```

### Tests

```bash
dotnet test OpenGitBase.sln
```

Compose-backed E2E scenarios are **not** part of solution-wide `dotnet test` by default — they run via `dotnet run --project tests/OpenGitBase.E2E.Runner` (see [tests/OpenGitBase.E2E/README.md](tests/OpenGitBase.E2E/README.md)).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). All commits in PRs must include DCO
sign-off (`git commit -s`).
