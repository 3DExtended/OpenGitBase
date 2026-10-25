# lfs-01 handoff — Tracer bullet: git lfs push and clone round trip

- PRD: `docs/prd/git-lfs.md` (#234); slice: `docs/issues/git-lfs/01-tracer-bullet-git-lfs-push-and-clone-round-trip.md` (#235)
- Dependencies: none
- Branch: `claude/lfs-implementation-difficulty-1a3fd2`

## Codebase facts gathered before implementation

- Dispatcher → API calls carry no auth; trust = `InternalNetworkMiddleware` IP allowlist with explicit
  `InternalNetworkOptions.RestrictedPathPrefixes` (add `/api/v1/internal/lfs`). HAProxy forwards `/api/v1/*`
  unchanged, so the prefix list is the only guard.
- Access-check logic lives inline in `RepositoryAccessChecksController` (PAT → user, owner/org/member roles,
  write-quorum, routing). Reused for LFS through `RepositoryAccessCheckEvaluator` (extracted, controller
  delegates; constructor unchanged so the 1100-line controller test suite keeps working).
- API → storage internal HTTP: `http://{InternalHost}:{InternalHttpPort}` + `Authorization: Bearer <node token>`
  (`GetStorageNodeApiTokenQuery`). Routing: `RepositoryReplicationRoutingQuery` → `RepositoryRoutingTargetDto`
  (has `StorageNodeId`, `InternalHttpPort`, `InternalGitHttpPort`, `Role`, `IsHealthy`, `IsPrimary`).
- Dispatcher reaches storage only on the git HTTP port 8082 (nginx). Storage python server runs on 8081
  (Bearer), as root, `ThreadingHTTPServer`, HTTP/1.0, `_read_body` reads whole body (don't reuse for LFS).
- `import-bundle` rmtree's `/srv/git/{id}.git` → LFS plaintext root must be outside: `/srv/git/.lfs/{id}/aa/bb/oid`.
- Role files: `ROLE_DIR/{repositoryId}.txt` ∈ Primary | Replica | ReadReplica | EncryptedReplica.
- JWT key: `JwtOptions.Key` singleton; LFS action token = custom HMAC token (not a JWT, so JwtBearer can't accept it).
- Tests: API uses xUnit + NSubstitute + SQLite in-memory; dispatcher tests use `DefaultHttpContext` + `StubHandler`;
  storage tests are stdlib `unittest`, run from `applications/repo-storage-layer` (`python3 -m unittest`).
- E2E tests run on the host (git from host PATH; git-lfs 3.7.1 installed locally), compose via
  `tests/OpenGitBase.E2E.Runner`. `PatPushCloneTests` is the prior art.
