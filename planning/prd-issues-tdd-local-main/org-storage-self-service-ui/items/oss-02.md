# Org storage bootstrap script

## Metadata

- ID: oss-02
- Type: AFK
- Status: ready
- Source: docs/prd/org-storage-self-service-ui.md

## Parent

[PRD: Organization Storage Self-Service UI](../../../../docs/prd/org-storage-self-service-ui.md)

## What to build

Ship a canonical **`bootstrap-org-storage-node.sh`** script in the repository scripts directory that installs an org-contributed storage node on a **generic Linux machine** with Docker, git, openssl, and curl.

**CLI inputs (required unless noted):**

- Enrollment token
- Node ID (must match enrollment)
- API base URL (instance control plane)
- Internal host — hostname or IP dispatchers use to reach this node
- Optional port overrides for SSH, internal HTTP, git HTTP, peer mTLS (defaults: 22, 8081, 8082, 8443)

**Script flow:**

1. Verify prerequisites and print actionable errors if missing.
2. Shallow-clone the OpenGitBase repository.
3. Generate per-node PKI (CA + node certificate/key) for the enrolled node ID.
4. Fetch fleet dispatcher SSH public key from the storage-node bootstrap API using enrollment token + node ID headers.
5. Build the storage agent container image from the repo-storage-layer Dockerfile.
6. Run the container with published ports, cert mounts, repos data volume, and enrollment environment variables (`STORAGE_ENROLLMENT_TOKEN`, `STORAGE_NODE_ID`, `STORAGE_INTERNAL_HOST`, etc.).

v1 uses **clone + local build** — no pre-published container registry image.

Support idempotent re-runs where reasonable (skip cert generation if files exist, replace container with same name).

## Acceptance criteria

- [ ] Script exits non-zero with clear message when docker/git/openssl/curl missing
- [ ] Script fails fast on missing required arguments
- [ ] Script generates PKI material for the specified node ID
- [ ] Script obtains dispatcher SSH public key via bootstrap API (no admin credentials)
- [ ] Script builds storage agent image and starts a container
- [ ] Storage agent registers with API and appears as org-owned node after successful run (manual verification against running instance documented in progress log)
- [ ] `--help` documents all flags including optional port overrides
- [ ] Script is safe to reference from UI as `curl … | bash -s -- …` one-liner (stable raw URL path documented for oss-03/oss-04)

## Blocked by

- None — can start immediately

## User stories covered

- 18 — Bootstrap script on generic Linux with Docker
- 19 — Shallow clone + build storage agent image
- 20 — Generate node PKI for registration thumbprint
- 21 — Fetch dispatcher SSH key via enrollment token
- 22 — Specify internal host for dispatcher reachability
- 23 — Default ports with optional overrides
- 33 — Bootstrap dispatcher-key endpoint requires valid enrollment

## Notes

- Adapt PKI generation logic from existing fleet PKI tooling; org nodes use a local CA per host (not platform compose PKI directory).
- Do not reference platform `docker-compose.override.yml` patterns — external org hosts are not compose fleet members.
- Full docker build in CI may be too heavy; optional `--dry-run` or argument-parsing unit test is acceptable for automated coverage.
- Manual test plan: clean Ubuntu/Debian VM, instance API reachable, enrollment token from API or test helper.
