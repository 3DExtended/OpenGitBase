# `ogb issue create`

## Metadata

- ID: cli-08
- Type: AFK
- Status: ready
- Source: docs/prd/ogb-cli.md

## Parent

[PRD: `ogb` CLI (Forge Command-Line Tool)](../../../docs/prd/ogb-cli.md)

## What to build

Introduce `IOgbApiClient` and implement `ogb issue create`, the first Discussion API integration. Accept `--title` (required) and optional `--body` or `--body-file`. Send `POST /repository/by-slug/{owner}/{slug}/discussions` with Bearer auth from the credential store. Print human-readable success output including discussion number and web URL. Map API `401` to a clear “session expired — run `ogb auth login`” message.

## Acceptance criteria

- [ ] `ogb issue create --title "…"` creates a Discussion and prints number + URL
- [ ] `--body` and `--body-file` supported for opening body
- [ ] Uses repo context from cli-07 (`-R` or git remote)
- [ ] Missing auth token fails with actionable message before API call
- [ ] API 401/403 propagate with user-facing messages matching web semantics
- [ ] Integration or HTTP handler tests verify request shape and Bearer header

## Blocked by

- [cli-07](./cli-07.md)

## User stories covered

- 18 — expired JWT fails with re-login guidance
- 19 — 401 from API triggers same guidance
- 23 — create discussion with title
- 24 — optional body / body-file
- 25 — print number and URL on success
- 26 — authorization failures match API
- 41 — human-friendly default output

## Notes

- CLI user-facing term is “issue”; API domain remains Discussion.
- Tags, assignee, and anchors are out of scope.
