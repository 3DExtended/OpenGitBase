# `ogb issue view`

## Metadata

- ID: cli-12
- Type: AFK
- Status: ready
- Source: docs/prd/ogb-cli.md

## Parent

[PRD: `ogb` CLI (Forge Command-Line Tool)](../../../docs/prd/ogb-cli.md)

## What to build

Implement `ogb issue view {number}` fetching discussion detail with comments included (`include=comments`). Display title, status, creator, assignee (if present), timestamps, tags (if present), and the full comment thread in human-readable format.

## Acceptance criteria

- [ ] `ogb issue view 42` shows metadata and comment thread
- [ ] 404 when discussion number not found
- [ ] Read access enforced per API (404/403 semantics for private repos)
- [ ] HTTP tests verify include=comments query

## Blocked by

- [cli-08](./cli-08.md)

## User stories covered

- 37 — view full discussion context in terminal
- 38 — comments loaded in same command

## Notes

- Markdown rendering in terminal can be plain text / minimal formatting in v1.
