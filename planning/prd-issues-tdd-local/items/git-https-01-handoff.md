# TDD handoff — git-https-01

## Work item

- **ID:** git-https-01
- **Title:** Git access tokens + settings UI + git config
- **File:** `docs/issues/git-https-pat/01-git-access-tokens-and-settings-ui.md`
- **Branch:** `feat/git-https-pat`
- **Base:** `main`

## Direct dependencies

- None (root)

## Full chain

`git-https-01`

## TDD scope

1. Git Access Token backend feature (entity, migration, CRUD, validation query)
2. `GET /api/v1/git/config` (`gitBaseUrl`, `sshEnabled`)
3. Web PAT settings page
4. Tests: handlers + API controller

## Prior art

- `features/public-git-ssh-key/` — feature structure
- `applications/opengitbase-web/app/pages/settings/ssh-keys.vue` — UI patterns
- `PublicGitSshKeyControllerTests` — API tests

## Out of scope for this item

- Repository access-check PAT integration (git-https-02)
- Dispatcher / storage git HTTP
