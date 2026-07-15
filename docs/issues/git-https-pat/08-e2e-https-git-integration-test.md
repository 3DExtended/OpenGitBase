<!-- forge: #143 -->

# End-to-end HTTPS git integration test

## Metadata

- ID: git-https-08
- Type: AFK
- Status: ready
- Source: docs/prd/git-https-personal-access-tokens.md

## Parent

[PRD: Git HTTPS via Personal Access Tokens](../../prd/git-https-personal-access-tokens.md)

## What to build

Add a compose-based shell integration test (prior art: `e2e-git-proxy-test.sh`) that exercises the full HTTPS git path: register user, create repository via API, create PAT, `git push` over HTTPS through HAProxy, `git clone` over HTTPS, verify commit content. Include a case where read-scoped PAT is denied on push.

## Acceptance criteria

- [ ] Script runs against running `docker compose` stack
- [ ] Creates user, repo, and write-scoped PAT via API
- [ ] `git push` over HTTPS through HAProxy succeeds
- [ ] `git clone` over HTTPS returns pushed content
- [ ] Read-scoped PAT denies push with clear failure
- [ ] Script documented in repo-storage-layer scripts or top-level scripts README
- [ ] CI or developer docs mention how to run the test

## Blocked by

- [05-haproxy-unified-http-routing.md](./05-haproxy-unified-http-routing.md)
- [07-https-clone-urls-and-repo-ui.md](./07-https-clone-urls-and-repo-ui.md)

## User stories covered

- 41, 42, 1, 2, 3

## Notes

- Test HTTPS path, not SSH. Use `GIT_SSL_NO_VERIFY` or local CA only if needed for self-signed local TLS; prefer HTTP through HAProxy if TLS terminates at Cloudflare only.
