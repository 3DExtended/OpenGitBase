<!-- forge: #89 -->

# Scenario catalog + authoring checklist

## Metadata

- ID: pop-01
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-test-population.md

## Parent

[PRD: E2E Test Population](../../prd/e2e-test-population.md)

## What to build

Create the **living scenario catalog** and **authoring checklist** for the E2E population program.

1. **Catalog file** — table of all E2E scenarios with columns: local ID (`E2E-F07-012` format), feature domain, scenario name, tags (`Smoke`/`Regression`/`FullHa`), PRD story refs, parity issue ref, status (`pending`/`in-progress`/`done`), owner.
2. Seed catalog with **existing 12 compose scenarios** marked `done`.
3. **Authoring checklist** in framework README: transcript, `BeginScenario`, baseline capture, traits, catalog row, PRD trace, `--update-baselines` workflow.
4. **Quality gates** doc section: what counts as meaningful E2E vs filler; rejection criteria from PRD.

End-to-end: a contributor can add a scenario and know exactly what artifacts to update; reviewers can see coverage gaps without reading the test assembly.

## Acceptance criteria

- [ ] `docs/e2e/scenario-catalog.md` exists with seeded rows for current E2E tests
- [ ] Catalog template row documented for new scenarios
- [ ] Authoring checklist added to E2E framework README
- [ ] Quality gates (reject/encourage criteria) documented
- [ ] Catalog IDs follow `E2E-<Feature>-<nnn>` convention

## Blocked by

- None — can start immediately

## User stories covered

- 1, 2, 3, 13, 122

## Notes

- Catalog is updated in the **same PR** as new scenario implementations (process rule, not automation v1).
- Blocks report feature rollup (pop-06) which consumes catalog metadata.
