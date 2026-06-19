# Git HTTPS via Personal Access Tokens — implementation issues

Vertical slices for [PRD: Git HTTPS via Personal Access Tokens](../../prd/git-https-personal-access-tokens.md).

Implement in order on branch `feat/git-https-pat`; each issue is blocked by the ones above it.

| # | ID | Issue | Type | Blocked by |
|---|-----|-------|------|------------|
| 1 | `git-https-01` | [Git access tokens + settings UI + git config](./01-git-access-tokens-and-settings-ui.md) | AFK | — |
| 2 | `git-https-02` | [PAT repository access-check + storage HTTP routing](./02-pat-repository-access-check.md) | AFK | 1 |
| 3 | `git-https-03` | [Storage git-http-backend](./03-storage-git-http-backend.md) | AFK | 2 |
| 4 | `git-https-04` | [Dispatcher Smart HTTP edge](./04-dispatcher-smart-http-proxy.md) | AFK | 2, 3 |
| 5 | `git-https-05` | [HAProxy unified HTTP routing + Cloudflare tunnel](./05-haproxy-unified-http-routing.md) | AFK | 4 |
| 6 | `git-https-06` | [SSH disable gate](./06-ssh-disable-gate.md) | AFK | 1, 5 |
| 7 | `git-https-07` | [Repository HTTPS clone URLs + settings navigation](./07-https-clone-urls-and-repo-ui.md) | AFK | 1, 6 |
| 8 | `git-https-08` | [End-to-end HTTPS git integration test](./08-e2e-https-git-integration-test.md) | AFK | 5, 7 |
