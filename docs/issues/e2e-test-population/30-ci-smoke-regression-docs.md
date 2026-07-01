# CI smoke vs regression documentation

## Metadata

- ID: pop-30
- Type: HITL
- Status: ready
- Source: docs/prd/e2e-test-population.md

## Parent

[PRD: E2E Test Population](../../prd/e2e-test-population.md)

## What to build

**Human sign-off** on CI strategy for smoke vs regression E2E — documentation and optional workflow stubs only (no mandatory GitHub Actions implementation in this slice).

Deliverables:

1. **CI decision doc** section in E2E README or `docs/e2e/ci-strategy.md`:
   - PR gate: `@Smoke` + unit tests
   - Nightly: full `@Regression` fast profile
   - Weekly/nightly HA: `@FullHa` + `--profile full-ha`
   - Playwright tier 8 nightly
2. **Runtime budget** estimates per job (from PRD targets).
3. **Machine requirements** (Docker, bootstrap, disk, RAM for full-ha).
4. **Explicit deferrals** — what stays manual until CI resources exist.
5. Team review sign-off recorded in doc (reviewer name/date section).

## Acceptance criteria

- [ ] CI strategy document written with clear job matrix
- [ ] Commands reference pop-05 `--tag` filters
- [ ] HITL review completed (name/date in doc)
- [ ] Out of scope for v1 explicitly stated (no required GHA merge blocker unless team decides)

## Blocked by

- [05-runner-tag-feature-filters.md](./05-runner-tag-feature-filters.md)

## User stories covered

- 20–24

## Notes

- HITL because release engineering owns CI policy and resource allocation.
- Framework parent PRD keeps CI implementation out of scope for v1.
