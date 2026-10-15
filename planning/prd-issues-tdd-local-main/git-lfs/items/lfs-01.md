# lfs-01 — Tracer bullet: git lfs push and clone round trip

## Summary

End-to-end Git LFS over HTTPS with the basic transfer adapter, plaintext on the primary only:

- **Storage:** `storage_lfs.py` — content-addressed plaintext store at `/srv/git/.lfs/{repoId}/aa/bb/{oid}`
  (outside the bare repo, so `import-bundle` cannot wipe it; repo-path validators refuse the LFS root),
  streaming PUT with SHA-256/size verification (temp file + fsync + `os.replace`, idempotent), GET/HEAD.
  Served to the dispatcher by a loopback-only transfer server (127.0.0.1:8083) behind nginx :8082
  (`/lfs/{repoId}/objects/{oid}`, request buffering off), and to the API on :8081
  (`HEAD/GET /internal/lfs/{repoId}/objects/{oid}`, Bearer). Encrypted replicas refuse transfers (403).
- **API:** `POST /api/v1/internal/lfs/{batch,transfer-check,verify}` (`LfsInternalController` →
  `LfsService`), guarded by the internal-network allowlist. Batch reuses the git access rules through the
  extracted `RepositoryAccessCheckEvaluator` (PAT, owner/org/member roles, write quorum). Actions carry a
  short-lived HMAC action token (`LfsActionTokenService`, key derived from `Jwt:Key`) bound to repo, oid,
  size and operation. Verify HEADs the primary and records the object (`LfsObject` table, PK
  `(RepositoryId, Oid)`, migration `AddLfsObjects`). Feature flag `Lfs:Enabled` (default off → 501).
- **Dispatcher:** `LfsPathParser` + `LfsHandler` intercept `{owner}/{repo}.git/info/lfs/*` before the
  Smart HTTP proxy: batch relay (`application/vnd.git-lfs+json`, `WWW-Authenticate` on 401), streaming
  upload with the request body limit set to exactly the declared size (Content-Length must match → 422,
  missing → 411), streaming download with target fallback on 404, verify relay, `locks/verify` stub
  (`{"ours":[],"theirs":[]}`) so git-lfs never prompts about the locking API.

### Deviation from the slice text

Action `href`s use `Lfs:PublicBaseUrl` when set, otherwise the base URL the dispatcher derives from the
client request (`X-Forwarded-Proto`/`X-Forwarded-Host`, falling back to the request's scheme/host), and
only then `Git:PublicBaseUrl`. The slice named `Git:PublicBaseUrl`; that breaks clients that reach the
forge through another host/port (E2E on a remapped port, LAN access), so the request-derived URL wins.

## Linked Context

- PRD: `docs/prd/git-lfs.md` (forge #234)
- Work item: `lfs-01` (forge #235)

## Dependency Graph

### Direct dependencies (blocked by)

- None

### Full chain

`lfs-01`

## Status

- Branch: `claude/lfs-implementation-difficulty-1a3fd2`
- Tests:
  - `dotnet test tests/OpenGitBase.Api.Tests` — 737 passed
  - `dotnet test tests/OpenGitBase.Features.Repository.Tests` — 66 passed
  - `dotnet test tests/OpenGitBase.Common.Tests` — 89 passed
  - `dotnet test tests/OpenGitBase.Dispatcher.Tests` — 56 passed
  - `dotnet test tests/OpenGitBase.E2E.Tests` (no stack: meta tests) — 27 passed
  - `python3 -m unittest` in `applications/repo-storage-layer` — 126 passed (pre-existing
    `test_storage_artifact_crypto` import error: host lacks `cryptography`)
  - Compose E2E: `LfsPushCloneTests.LfsPushAndCloneRoundTripByteIdentical` passed (5 MB object, pointer
    committed, PUT on push, byte-identical clone, second branch push uploads nothing);
    `PatPushCloneTests` passes functionally (its baseline embeds the checkout path, which differs in a
    worktree — unrelated to LFS)
- Visual snapshots: none
- Commit(s): see `progress-log.md`

### Local compose note

The E2E stack ran with a scratch overlay: containers renamed `ogblfs_*`
(`OPENGITBASE_E2E_CONTAINER_PREFIX=ogblfs_`), HAProxy on 18089 (`OPENGITBASE_E2E_HTTP_PORT=18089`;
8089 is taken on the dev machine), and named volumes instead of the Tower-only Kafka bind mounts
(`/mnt/cache/appdata/opengitbase/kafka*`, since #232), which Docker Desktop cannot mount.
