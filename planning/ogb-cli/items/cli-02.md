# Host resolver and config store

## Metadata

- ID: cli-02
- Type: AFK
- Status: ready
- Source: docs/prd/ogb-cli.md

## Parent

[PRD: `ogb` CLI (Forge Command-Line Tool)](../../../docs/prd/ogb-cli.md)

## What to build

Implement `IHostResolver` and `IConfigStore` deep modules. The CLI defaults to `https://www.opengitbase.com/` and accepts a global `--hostname` override. Persist active host metadata in XDG config (`~/.config/ogb/hosts.yml`) with file mode `0600`. Host normalization covers scheme, trailing slashes, and API base path conventions matching the web client.

## Acceptance criteria

- [ ] Default host is `https://www.opengitbase.com/` when no config or flag is set
- [ ] `--hostname` overrides the active host for the current invocation
- [ ] Config file stores active hostname (not JWT) with mode `0600`
- [ ] Unit tests cover normalization edge cases (with/without scheme, trailing slash)

## Blocked by

- [cli-01](./cli-01.md)

## User stories covered

- 4 — default production host
- 5 — `--hostname` override for self-hosted instances
- 6 — active hostname recorded after login (config write wired in cli-04; read path works here)

## Notes

- Verify API path prefix against existing HAProxy/Caddy routing (`/api` vs bare paths) before locking normalization rules.
