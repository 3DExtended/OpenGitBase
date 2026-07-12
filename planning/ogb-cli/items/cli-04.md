# Loopback listener and `ogb auth login`

## Metadata

- ID: cli-04
- Type: AFK
- Status: ready
- Source: docs/prd/ogb-cli.md

## Parent

[PRD: `ogb` CLI (Forge Command-Line Tool)](../../../docs/prd/ogb-cli.md)

## What to build

Implement `ILoopbackAuthServer` and the `ogb auth login` command. The CLI binds an ephemeral port on `127.0.0.1`, generates a cryptographically random `state` value, opens the system browser to `{host}/cli/auth?port=…&state=…`, and waits for a callback at `/callback` with matching `state` and JWT. Store the JWT in an in-memory `ICredentialStore` implementation and persist the active host via `IConfigStore`. Reject callbacks with missing or mismatched `state`.

## Acceptance criteria

- [ ] `ogb auth login` starts localhost listener and opens browser to `/cli/auth`
- [ ] Callback with valid `state` captures JWT and exits successfully
- [ ] Callback with invalid/missing `state` is rejected
- [ ] JWT stored in credential store keyed by normalized hostname; host metadata in config file
- [ ] Unit tests for loopback server state validation and single-successful-callback semantics
- [ ] Manual smoke: end-to-end login against local compose stack with cli-03 page

## Blocked by

- [cli-02](./cli-02.md)
- [cli-03](./cli-03.md)

## User stories covered

- 7 — browser-based login
- 9 — automatic return to CLI after login
- 10 — random `state` validated on callback
- 11 — redirect only to localhost on advertised port
- 14 — log in once for subsequent commands
- 15 — stored credentials available to non-interactive invocations on same machine

## Notes

- OS keychain storage lands in cli-06; in-memory store is sufficient for this slice.
- Document manual smoke steps in PR or item handoff.
