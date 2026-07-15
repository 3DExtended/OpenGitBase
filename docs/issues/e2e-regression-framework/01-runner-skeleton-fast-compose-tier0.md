<!-- forge: #70 -->

# Runner skeleton + fast compose + Tier 0 smoke

## Metadata

- ID: e2e-01
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-regression-framework.md

## Parent

[PRD: Unified E2E Regression Framework](../../prd/e2e-regression-framework.md)

## What to build

Scaffold the E2E runner as a thin C# console executable — the documented local entry point for regression. Wire solution projects: runner host, shared support library, and empty test assembly shell.

On `dotnet run` (default):

1. Parse minimal CLI (`--profile fast` default, `--no-open-report` accepted but no-op until report slice lands).
2. Start Docker Compose with a **fast profile** (minimal services sufficient for API health — postgres, redis, API, storage, dispatcher, HAProxy as needed).
3. Wait for health endpoints.
4. Host or subprocess-invoke xUnit with one **Tier 0** test: stack is up, migrations applied, API `/health` returns healthy.
5. Write a stub local report directory and set exit code non-zero on Tier 0 failure.

No baselines, transcripts, or browser open in this slice — prove the orchestration spine only.

## Acceptance criteria

- [ ] Runner project added to solution and documented as the E2E entry point
- [ ] Fast compose profile starts stack and waits for health without manual `docker compose up`
- [ ] Tier 0 smoke test passes against running stack
- [ ] Tier 0 failure yields non-zero exit code
- [ ] Stub report path created under local reports directory (may be empty or minimal placeholder)
- [ ] `--profile fast` is default; `--profile full-ha` accepted but may no-op until e2e-14

## Blocked by

- None — can start immediately

## User stories covered

- 1, 2, 3, 37, 67

## Notes

- Follow PRD module boundaries: runner must not embed baseline or HTML logic yet.
- Fast profile exact service set should match existing compose capabilities; prefer compose override files over ad-hoc container manipulation.
- Prior art: existing shell e2e scripts assume compose already up — this slice inverts that.
