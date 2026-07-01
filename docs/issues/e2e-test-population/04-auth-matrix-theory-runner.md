# Auth matrix theory runner

## Metadata

- ID: pop-04
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-test-population.md

## Parent

[PRD: E2E Test Population](../../prd/e2e-test-population.md)

## What to build

Implement a **table-driven auth matrix runner** for bulk E2E scenario generation.

1. **Case record**: actor (anonymous/outsider/reader/writer/admin/owner), HTTP method, relative URL, optional body, expected status, baseline capture name.
2. **Runner** integrates with `E2eApiClient`, `OperationTranscript`, `BaselineManager` — one theory row = one baseline subfolder via `BeginScenario(scopeSuffix)`.
3. **Skip or N/A** handling for inapplicable cells documented.
4. Reference implementation: expand `AuthMatrixTests` from 1 case to a reusable base class other features subclass.

Verifiable: one feature (e.g. repository members) adds 10+ matrix rows with committed baselines in a single theory class.

## Acceptance criteria

- [ ] `AuthMatrixCase` (or equivalent) type and runner in E2E core
- [ ] Theory test demonstrates ≥10 matrix rows with baselines
- [ ] Transcript records intent per row ("Outsider cannot DELETE member")
- [ ] Documented pattern in authoring checklist for feature teams

## Blocked by

- [02-shared-fixture-library.md](./02-shared-fixture-library.md)

## User stories covered

- 9, 51, 75 (matrix pattern for all features)

## Notes

- Primary mechanism to reach ~50 scenarios per feature without copy-paste.
