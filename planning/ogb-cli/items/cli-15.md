# Exit codes and structured errors

## Metadata

- ID: cli-15
- Type: AFK
- Status: ready
- Source: docs/prd/ogb-cli.md

## Parent

[PRD: `ogb` CLI (Forge Command-Line Tool)](../../../docs/prd/ogb-cli.md)

## What to build

Ensure all commands return non-zero exit codes on failure (auth errors, network errors, HTTP 4xx/5xx, validation errors). When `--json` is set, emit structured error objects including HTTP status and API error body when available. stderr carries human errors when not using JSON.

## Acceptance criteria

- [ ] Success exits 0; failures exit non-zero
- [ ] JSON error shape includes `error`, `httpStatus` (when applicable), and `detail` from API body
- [ ] Session expired and missing-auth cases exit non-zero with consistent messages
- [ ] Tests cover exit codes for: missing repo context, 401, 403, 404, validation failure

## Blocked by

- [cli-14](./cli-14.md)

## User stories covered

- 43 — JSON errors include HTTP status and API body
- 44 — non-zero exit codes for script/CI visibility

## Notes

- Completes v1 scripting story alongside cli-14; CI pipeline token auth remains out of scope.
