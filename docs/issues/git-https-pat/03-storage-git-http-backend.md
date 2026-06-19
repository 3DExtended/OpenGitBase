# Storage git-http-backend

## Metadata

- ID: git-https-03
- Type: AFK
- Status: ready
- Source: docs/prd/git-https-personal-access-tokens.md

## Parent

[PRD: Git HTTPS via Personal Access Tokens](../../prd/git-https-personal-access-tokens.md)

## What to build

Add Git Smart HTTP serving on storage nodes via `git-http-backend` on internal port `:8082`, separate from the existing provisioning HTTP API on `:8081`. Storage trusts the private Docker network only — no per-user token validation on storage.

Update storage container entrypoint to start the git HTTP service alongside `storage-http-server.py` and optional `sshd`. Map requests to bare repos under `/srv/git` using canonical physical paths.

Add a shell integration test: provision a bare repo via internal API, then `git clone` over the storage node's internal git HTTP port.

## Acceptance criteria

- [ ] Git HTTP listens on configurable internal port (default 8082)
- [ ] `git-http-backend` serves bare repos under `/srv/git`
- [ ] Provisioning API on 8081 unchanged and isolated
- [ ] Storage healthcheck includes git HTTP process
- [ ] Integration test: provision repo → clone via internal git HTTP succeeds
- [ ] Git HTTP not exposed on host ports in default compose profile

## Blocked by

- [02-pat-repository-access-check.md](./02-pat-repository-access-check.md)

## User stories covered

- 24, 25, 26

## Notes

- Prefer official `git-http-backend` CGI pattern over custom protocol implementation.
- `fcgiwrap` or equivalent is acceptable for MVP.
