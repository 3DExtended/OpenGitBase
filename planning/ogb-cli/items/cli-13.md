# `ogb issue status`

## Metadata

- ID: cli-13
- Type: AFK
- Status: ready
- Source: docs/prd/ogb-cli.md

## Parent

[PRD: `ogb` CLI (Forge Command-Line Tool)](../../../docs/prd/ogb-cli.md)

## What to build

Implement `ogb issue status {number}` as a minimal read command that prints only the discussion lifecycle state: `Open`, `Engaged`, `Resolved`, or `Dismissed`. Uses discussion detail fetch without loading full comment thread when a lighter endpoint suffices.

## Acceptance criteria

- [ ] `ogb issue status 42` prints exactly one status enum value on success
- [ ] Unknown number returns non-zero exit with clear error
- [ ] Suitable for shell scripting gate checks (before cli-14 adds `--json`)

## Blocked by

- [cli-08](./cli-08.md)

## User stories covered

- 39 — status-only output for scripting
- 40 — basis for JSON status in cli-14

## Notes

- Human single-line output in this slice; structured JSON in cli-14.
