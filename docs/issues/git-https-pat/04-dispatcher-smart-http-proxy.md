<!-- forge: #139 -->

# Dispatcher Smart HTTP edge

## Metadata

- ID: git-https-04
- Type: AFK
- Status: ready
- Source: docs/prd/git-https-personal-access-tokens.md

## Parent

[PRD: Git HTTPS via Personal Access Tokens](../../prd/git-https-personal-access-tokens.md)

## What to build

Turn the dispatcher into a long-lived HTTP server (Kestrel) that handles Git Smart HTTP at `/{ownerSlug}/{repoSlug}.git/...`. Parse Smart HTTP operations (`info/refs`, `git-upload-pack`, `git-receive-pack`) and map to repository operations.

Extract PAT from HTTP Basic auth (password = token). Call repository access-check with token + path + operation. On denial return `401`/`403` before proxying. On success reverse-proxy request and response streams to the storage node's internal git HTTP port using canonical `physicalPath` from access-check.

Update dispatcher entrypoint to start Kestrel always; keep existing per-SSH-session `Program.cs` path intact for when SSH is re-enabled later.

## Acceptance criteria

- [ ] Dispatcher listens on configurable HTTP port (default 8082)
- [ ] Parses `/{owner}/{repo}.git/info/refs`, `git-upload-pack`, `git-receive-pack`
- [ ] Extracts PAT from Basic auth and calls access-check API
- [ ] Denied access returns 401/403 without contacting storage
- [ ] Allowed requests proxied to correct storage node git HTTP port and physical path
- [ ] Response streams unchanged (content types, pack data)
- [ ] Unit tests for path parsing and operation mapping
- [ ] Manual verification: `git clone` and `git push` against dispatcher HTTP port with PAT

## Blocked by

- [02-pat-repository-access-check.md](./02-pat-repository-access-check.md)
- [03-storage-git-http-backend.md](./03-storage-git-http-backend.md)

## User stories covered

- 1, 2, 3, 4, 5, 8, 17, 20, 21, 22, 23

## Notes

- Slug resolution stays in API; dispatcher uses `physicalPath` from access-check response.
- Do not implement HAProxy routing in this slice — direct dispatcher port is sufficient for verification.
