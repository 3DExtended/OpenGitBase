<!-- forge: #117 -->

# Playwright behavioral regression specs

## Metadata

- ID: pop-29
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-test-population.md

## Parent

[PRD: E2E Test Population](../../prd/e2e-test-population.md)

## What to build

Expand Playwright **`@regression`** tier beyond visual shell snapshots with **behavioral UI scenarios**.

Minimum deliverables:

1. **10+ new `@regression` specs** covering: sign-in flow (MSW), repo overview navigation, discussion list render, MR list render, explore page, admin replication page (mocked API).
2. Specs use **MSW** and stable `data-testid` selectors — not pixel baselines in git.
3. Playwright artifacts continue embedding in unified E2E HTML report.
4. Catalog rows for UI scenarios linked to F01, F05, F06, F07, F11, F12.
5. Document how UI regression complements C# E2E (API/git proofs stay in C#).

Verifiable: `dotnet run … -- --tier 8` passes with expanded regression tag set.

## Acceptance criteria

- [ ] ≥10 new `@regression` Playwright tests (in addition to existing shell visual specs)
- [ ] All tagged `@regression` green in tier 8
- [ ] Catalog documents UI scenario IDs
- [ ] README clarifies visual vs behavioral Playwright specs

## Blocked by

- [01-scenario-catalog-authoring-checklist.md](./01-scenario-catalog-authoring-checklist.md)

## User stories covered

- 110 (admin UI optional), 111–112 (explore/profile), framework stories 23–26

## Notes

- Do not commit Playwright pixels as C# baselines.
- Parallel with C# population waves after pop-01.
