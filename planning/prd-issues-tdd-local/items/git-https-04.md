# git-https-04 — implementation record

## Status

- Branch: `feat/git-https-pat`
- Base branch: `main`
- Tests: `OpenGitBase.Dispatcher.Tests` — 17 passing
- Completion: **done** (issue 04 scope)

## Summary

Dispatcher runs Kestrel Smart HTTP on configurable port 8082 alongside sshd. PAT extracted from HTTP Basic auth, validated via access-check API, then proxied to storage git HTTP using `physicalPath` routing.

- `--serve-http` mode started from entrypoint (SSH per-session path unchanged)
- `GitSmartHttpPathParser`, `GitSmartHttpHandler`, `GitHttpProxyService`
- `RepositoryAccessCheckClient.CheckWithTokenAsync`
- Compose: dispatcher-1 publishes `8822:8082` for local smoke tests

## Linked Context

- PRD: `docs/prd/git-https-personal-access-tokens.md`
- Work item: `docs/issues/git-https-pat/04-dispatcher-smart-http-proxy.md`

## Dependency Graph

### Direct dependencies (blocked by)

- git-https-02, git-https-03

### Full chain

`git-https-01` → `git-https-02` → `git-https-03` → `git-https-04`

## Tests

- `GitSmartHttpPathParserTests`, `GitHttpProxyServiceTests`, `BasicAuthTokenReaderTests`
- Manual: `scripts/git-http-smoke-test.sh` (requires compose stack)

## Notes

- HAProxy unified routing deferred to git-https-05
- Denied auth: 401 for invalid/expired token, 403 otherwise
