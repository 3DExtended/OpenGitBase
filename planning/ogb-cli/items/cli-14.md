# `--json` output

## Metadata

- ID: cli-14
- Type: AFK
- Status: ready
- Source: docs/prd/ogb-cli.md

## Parent

[PRD: `ogb` CLI (Forge Command-Line Tool)](../../../docs/prd/ogb-cli.md)

## What to build

Add a global `--json` flag and structured JSON output for `ogb auth status` and all issue subcommands (create, comment, close, list, view, status). Introduce `IOutputWriter` with stable property names (`number`, `title`, `status`, `url`, `comments`, etc.). Human output remains the default when `--json` is absent.

## Acceptance criteria

- [ ] `--json` on auth status emits `{ "loggedIn", "hostname", "username" }` (or equivalent stable shape)
- [ ] Each issue command emits parseable JSON on success
- [ ] JSON property names documented and stable across patch releases
- [ ] Default (non-JSON) output unchanged from prior slices
- [ ] Tests snapshot or assert JSON shapes for at least one command per group (auth + issue)

## Blocked by

- [cli-05](./cli-05.md)
- [cli-13](./cli-13.md)

## User stories covered

- 40 — `issue status --json` for automation
- 42 — `--json` on commands that produce data

## Notes

- Structured error JSON lands in cli-15.
