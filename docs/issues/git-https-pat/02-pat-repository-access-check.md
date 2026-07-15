<!-- forge: #137 -->

# PAT repository access-check + storage HTTP routing

## Metadata

- ID: git-https-02
- Type: AFK
- Status: ready
- Source: docs/prd/git-https-personal-access-tokens.md

## Parent

[PRD: Git HTTPS via Personal Access Tokens](../../prd/git-https-personal-access-tokens.md)

## What to build

Extend the repository access-check pipeline to accept either an SSH public key or a Personal Access Token (mutually exclusive). PAT path resolves the user via Git Access Token validation, then runs the existing slug resolution, membership, role, quota, and storage routing logic.

Enforce token scopes: `read` (or `write`) allows upload-pack; `write` required for receive-pack. Deny with clear reasons for wrong scope, expired, or revoked tokens.

Extend access-check allowed response with `storageNodeInternalHttpPort`. Extend storage node registry and heartbeat to carry internal git HTTP port (default 8082).

## Acceptance criteria

- [ ] Access-check accepts `accessToken` OR `publicKey`, not both
- [ ] Valid read-scoped PAT allows upload-pack when role permits read
- [ ] Valid write-scoped PAT allows receive-pack when role permits write
- [ ] Read-scoped PAT denies receive-pack before storage routing
- [ ] Expired/revoked/invalid PAT denies with authentication failure
- [ ] Allowed response includes `storageNodeInternalHttpPort`
- [ ] Storage node registration/heartbeat persists and reports git HTTP port
- [ ] Unhealthy assigned storage node still fails fast with clear reason
- [ ] API controller tests cover PAT paths, scopes, and routing fields

## Blocked by

- [01-git-access-tokens-and-settings-ui.md](./01-git-access-tokens-and-settings-ui.md)

## User stories covered

- 6, 7, 39, 40, 21 (partial)

## Notes

- Reuse existing access-check tests as prior art; extend rather than duplicate SSH scenarios.
