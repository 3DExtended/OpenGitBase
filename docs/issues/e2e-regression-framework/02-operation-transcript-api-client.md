<!-- forge: #71 -->

# Operation transcript + auto-logging API client

## Metadata

- ID: e2e-02
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-regression-framework.md

## Parent

[PRD: Unified E2E Regression Framework](../../prd/e2e-regression-framework.md)

## What to build

Add hybrid operation logging to the E2E test support library:

1. **IOperationTranscript** — records human intent lines via `Describe("…")` and automatic wire events.
2. **HTTP API client wrapper** — every request/response auto-records method, URL, status, and truncated body metadata without test-author boilerplate.
3. Export per-test transcript as structured JSON attached to test results for downstream report/baseline slices.

Deliver one Tier 0/1 smoke test that calls API health (or register) through the wrapper, adds one `Describe` line, and asserts transcript contains both intent and wire entries.

## Acceptance criteria

- [ ] Transcript captures human intent entries and wire-level HTTP events in order
- [ ] API client wrapper used by at least one integration test against compose stack
- [ ] Transcript serializes to JSON consumable by later report generator
- [ ] Unit tests cover transcript ordering and wire event shape
- [ ] Test authors are not required to manually log HTTP method/URL/status

## Blocked by

- [01-runner-skeleton-fast-compose-tier0.md](./01-runner-skeleton-fast-compose-tier0.md)

## User stories covered

- 10, 11, 12, 13

## Notes

- Wire event kinds to support in v1: `HttpRequest`, `HttpResponse`. Git, email, cluster events added in later slices.
- Keep interface stable — baseline and report modules depend on it next.
