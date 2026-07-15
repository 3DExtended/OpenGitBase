<!-- forge: #93 -->

# Runner tag and feature filters

## Metadata

- ID: pop-05
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-test-population.md

## Parent

[PRD: E2E Test Population](../../prd/e2e-test-population.md)

## What to build

Extend the E2E runner CLI to filter by **scenario tags** and **feature domain**.

1. **`--tag Smoke|Regression|FullHa`** — maps to xUnit trait filter combined with existing tier logic.
2. **`--feature <Name>`** — maps to `Category=<Name>` or namespace filter (e.g. `Discussion`, `MergeRequest`).
3. Compose with existing `--filter`, `--tier`, `--skip-compose`.
4. Document commands for daily smoke vs feature iteration in README.

Verifiable: `dotnet run --project tests/OpenGitBase.E2E.Runner -- --tag Smoke` runs only smoke-tagged tests.

## Acceptance criteria

- [ ] `--tag` implemented and documented
- [ ] `--feature` implemented and documented
- [ ] Existing `--tier` and `--filter` still work
- [ ] README examples for smoke and single-feature runs

## Blocked by

- None — can start immediately

## User stories covered

- 10, 11, 12, 20, 25

## Notes

- Requires `[Trait("Smoke")]` / `[Trait("Regression")]` on scenarios (applied in population slices).
- Parallel with pop-02.
