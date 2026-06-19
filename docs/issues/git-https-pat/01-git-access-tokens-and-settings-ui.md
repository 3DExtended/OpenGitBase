# Git access tokens + settings UI + git config

## Metadata

- ID: git-https-01
- Type: AFK
- Status: ready
- Source: docs/prd/git-https-personal-access-tokens.md

## Parent

[PRD: Git HTTPS via Personal Access Tokens](../../prd/git-https-personal-access-tokens.md)

## What to build

Introduce Personal Access Tokens as a first-class credential type. Users create named tokens with read or write scope and optional expiration (default 90 days; allow no expiration). Tokens use an `ogb_` prefix; only a hash is stored in the database. The raw token is returned exactly once at creation.

Expose authenticated CRUD API for the signed-in user's tokens. Add a validation query that resolves a raw token to user id and scopes (for later access-check integration).

Expose public git configuration: `gitBaseUrl` (e.g. `https://opengitbase.com`) and `sshEnabled` (mirrors `GIT_SSH_ENABLED`, default false).

Add a web settings page for PAT management mirroring the SSH keys page: create with name/scope/expiry, list metadata, revoke, one-time secret display on create. Link from settings navigation.

## Acceptance criteria

- [ ] `GitAccessToken` entity persisted with hash, scopes, optional expiry, revoke timestamp
- [ ] `POST /git-access-token` creates token and returns plaintext once
- [ ] `GET /git-access-token` lists current user's tokens without secrets
- [ ] `DELETE /git-access-token/{id}` revokes owner token
- [ ] Validation query resolves valid token to user id + scopes; rejects expired/revoked/invalid
- [ ] `GET /api/v1/git/config` returns `gitBaseUrl` and `sshEnabled`
- [ ] Web settings page: create, list, revoke, one-time reveal
- [ ] Query handler and API controller tests cover create, list, revoke, validation
- [ ] OpenAPI synced if project convention requires it

## Blocked by

- None — can start immediately

## User stories covered

- 11, 12, 13, 14, 15, 16, 18, 19, 36, 37, 38

## Notes

- Follow `PublicGitSshKey` feature patterns for CQRS, Mapster, and API controller structure.
- Use `agentGenCli new backend-feature` where appropriate.
- Do not wire PAT into repository access-check yet — that is issue 02.
