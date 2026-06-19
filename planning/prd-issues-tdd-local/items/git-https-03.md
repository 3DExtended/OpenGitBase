# git-https-03 — implementation record

## Status

- Branch: `feat/git-https-pat`
- Base branch: `main`
- Tests: `git-http-integration-test.sh` passing
- Completion: **done** (issue 03 scope)

## Summary

Added Git Smart HTTP on storage nodes via `git-http-backend` (nginx + fcgiwrap) on internal port 8082, separate from provisioning API on 8081.

- `storage-git-http.sh` + `nginx-git-http.conf` — fcgiwrap + nginx on `STORAGE_GIT_HTTP_PORT` (default 8082)
- `entrypoint.sh` — starts git HTTP alongside provisioning API; `STORAGE_STANDALONE=1` for integration tests
- `storage-http-server.py` — `chown git:git` after bare repo init for git-http-backend access
- `docker-compose.yml` — healthcheck includes nginx; git HTTP port env vars (not host-published)
- `scripts/git-http-integration-test.sh` — provision via :8081, clone via :8082

## Linked Context

- PRD: `docs/prd/git-https-personal-access-tokens.md`
- Work item: `docs/issues/git-https-pat/03-storage-git-http-backend.md`

## Dependency Graph

### Direct dependencies (blocked by)

- git-https-02

### Full chain

`git-https-01` → `git-https-02` → `git-https-03`

## Tests

- `applications/repo-storage-layer/scripts/git-http-integration-test.sh`

## Notes

- Storage does not validate user tokens; trusts private network only
- Dockerfile adds `nginx-light`, `fcgiwrap`, `spawn-fcgi`
