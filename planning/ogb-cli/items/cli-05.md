# `ogb auth status` and `ogb auth logout`

## Metadata

- ID: cli-05
- Type: AFK
- Status: ready
- Source: docs/prd/ogb-cli.md

## Parent

[PRD: `ogb` CLI (Forge Command-Line Tool)](../../../docs/prd/ogb-cli.md)

## What to build

Add `ogb auth status` and `ogb auth logout` subcommands. Status reports whether a JWT exists for the active host, the hostname, and the username decoded from JWT claims when possible. Logout removes the JWT from the credential store and clears active-host metadata from config for that host.

## Acceptance criteria

- [ ] `ogb auth status` shows logged-out state when no token is stored
- [ ] `ogb auth status` shows hostname and username when logged in
- [ ] `ogb auth logout` clears credential store and config for the active host
- [ ] After logout, `auth status` reports logged out
- [ ] Unit tests cover status output for logged-in and logged-out states

## Blocked by

- [cli-04](./cli-04.md)

## User stories covered

- 12 — `auth status` confirms session before scripting
- 13 — `auth logout` clears credentials on shared machines

## Notes

- Human-readable output only in this slice; `--json` for auth status lands in cli-14.
