# `ogb issue close`

## Metadata

- ID: cli-10
- Type: AFK
- Status: ready
- Source: docs/prd/ogb-cli.md

## Parent

[PRD: `ogb` CLI (Forge Command-Line Tool)](../../../docs/prd/ogb-cli.md)

## What to build

Implement `ogb issue close {number}`. Default action calls the **resolve** endpoint. `--reason dismissed` calls the **dismiss** endpoint. Require Writer+ role per API (Reader gets 403). Print resulting discussion status in human output on success.

## Acceptance criteria

- [ ] `ogb issue close 42` resolves the discussion
- [ ] `ogb issue close 42 --reason dismissed` dismisses the discussion
- [ ] Success output shows final status (`Resolved` or `Dismissed`)
- [ ] 403 for insufficient role; 401 for expired session
- [ ] HTTP tests verify resolve vs dismiss endpoint selection

## Blocked by

- [cli-08](./cli-08.md)

## User stories covered

- 30 — close defaults to resolve
- 31 — `--reason dismissed` for dismiss
- 32 — Reader cannot close (403)
- 33 — confirm resulting status in output

## Notes

- No generic “close” API exists; CLI maps to resolve/dismiss only.
