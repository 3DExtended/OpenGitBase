# `ogb issue comment`

## Metadata

- ID: cli-09
- Type: AFK
- Status: ready
- Source: docs/prd/ogb-cli.md

## Parent

[PRD: `ogb` CLI (Forge Command-Line Tool)](../../../docs/prd/ogb-cli.md)

## What to build

Implement `ogb issue comment {number}` with `--body` or `--body-file`. Call `POST …/discussions/{number}/comments` with Markdown body. Rely on existing API reopen-via-comment behavior for Resolved/Dismissed discussions (no separate reopen command). Human-readable success output.

## Acceptance criteria

- [ ] `ogb issue comment 42 --body "…"` posts a comment and confirms success
- [ ] `--body-file` reads comment text from disk
- [ ] Commenting on a closed discussion succeeds and reopens per API rules
- [ ] Authorization and session errors handled consistently with cli-08
- [ ] HTTP tests verify comment request payload

## Blocked by

- [cli-08](./cli-08.md)

## User stories covered

- 27 — comment from terminal/scripts
- 28 — body-file for long markdown
- 29 — reopen-via-comment unchanged

## Notes

- Parent comment / sub-thread flags are out of scope for v1.
