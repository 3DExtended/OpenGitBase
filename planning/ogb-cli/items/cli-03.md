# Nuxt `/cli/auth` page

## Metadata

- ID: cli-03
- Type: AFK
- Status: ready
- Source: docs/prd/ogb-cli.md

## Parent

[PRD: `ogb` CLI (Forge Command-Line Tool)](../../../docs/prd/ogb-cli.md)

## What to build

Add a Nuxt route at `/cli/auth` for the loopback login flow. The page reads `port` and `state` query parameters, displays a username/password form styled consistently with the web app, shows the target hostname, and POSTs credentials to the existing `POST /signin/login` endpoint. On success, redirect to `http://127.0.0.1:{port}/callback?token={jwt}&state={state}` with URL-encoded token. On failure, show an inline error without breaking the loopback session (user can retry). Do not surface Google/Apple OAuth or new-user registration redirect in v1.

## Acceptance criteria

- [ ] Page renders only when `port` and `state` query params are present; shows clear error otherwise
- [ ] Successful login redirects to localhost callback with matching `state` and JWT
- [ ] Invalid credentials show inline error; no redirect
- [ ] Page displays which host/instance the user is authenticating against
- [ ] Automated web test: valid params render form; successful login produces expected redirect URL

## Blocked by

- [cli-01](./cli-01.md)

## User stories covered

- 8 — username/password only on CLI auth page
- 45 — visually consistent with web app
- 46 — shows target hostname
- 47 — inline error on bad credentials without breaking flow
- 48 — clear message that an existing account is required

## Notes

- Reuse existing API client patterns from the Nuxt app for the login POST.
- Never store password in URL or client-side persistence.
