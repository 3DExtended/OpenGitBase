# Repo context (`-R` / git remote inference)

## Metadata

- ID: cli-07
- Type: AFK
- Status: ready
- Source: docs/prd/ogb-cli.md

## Parent

[PRD: `ogb` CLI (Forge Command-Line Tool)](../../../docs/prd/ogb-cli.md)

## What to build

Implement `IGitRemoteResolver` and shared repository context for issue subcommands. When run inside a git clone, infer `owner` and `slug` from the `origin` remote (HTTPS and SSH URL patterns). Accept `-R owner/repo` and `--repo` as explicit override. Register the repo option on the `issue` command group and return a clear error when context cannot be resolved.

## Acceptance criteria

- [ ] Parses `https://{host}/{owner}/{repo}.git` and `git@{host}:{owner}/{repo}.git` (with/without `.git`)
- [ ] `-R` / `--repo` takes precedence over git remote inference
- [ ] Clear error when outside a clone and no `-R` provided
- [ ] Unit tests for URL parsing edge cases and override precedence

## Blocked by

- [cli-04](./cli-04.md)

## User stories covered

- 20 — infer owner/repo from origin inside a clone
- 21 — `-R owner/repo` for explicit or cross-repo use
- 22 — clear error when context missing

## Notes

- Verify repo slug resolution against an issue subcommand stub or cli-08 smoke; no Discussion API calls required in this slice.
