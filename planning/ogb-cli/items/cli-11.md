# `ogb issue list`

## Metadata

- ID: cli-11
- Type: AFK
- Status: ready
- Source: docs/prd/ogb-cli.md

## Parent

[PRD: `ogb` CLI (Forge Command-Line Tool)](../../../docs/prd/ogb-cli.md)

## What to build

Implement `ogb issue list` calling `GET …/discussions` with optional `--status` filter (open/engaged/resolved/dismissed mapped to Discussion API enum). Render a human-readable table with at minimum number, title, status, and updated timestamp.

## Acceptance criteria

- [ ] Lists discussions for resolved repo context
- [ ] `--status` filter passed through to API query parameter
- [ ] Table columns: number, title, status, updated time
- [ ] Empty list handled gracefully
- [ ] HTTP tests verify list URL and query params

## Blocked by

- [cli-08](./cli-08.md)

## User stories covered

- 34 — list discussions from terminal
- 35 — optional status filter
- 36 — scannable table output

## Notes

- Tag and assignee filters are out of scope for v1.
