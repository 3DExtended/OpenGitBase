# URL discovery + Discovered skeleton generator

## Metadata

- ID: e2e-11
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-regression-framework.md

## Parent

[PRD: Unified E2E Regression Framework](../../prd/e2e-regression-framework.md)

## What to build

Implement coverage discovery and auto-generated tests:

1. **Link extraction** — during page capture or Playwright-assisted navigation, collect hrefs from HTML.
2. **Coverage registry** — track known covered URL patterns vs newly discovered routes.
3. **Skeleton generator** — emit C# test file under `Discovered/` with smoke visit + baseline hooks and auto-generation marker comments.
4. **Workflow** — discovered test fails until `--update-baselines`; report lists new URLs and missing baselines.

Vertical demo: test visits page containing uncaptured link → skeleton generated → run fails with update-baselines instruction → after update, test passes.

## Acceptance criteria

- [ ] Visited pages scanned for links not in coverage registry
- [ ] New URL generates skeleton test under `Discovered/` folder
- [ ] Generated test fails without committed baseline
- [ ] `--update-baselines` creates goldens for generated test
- [ ] Report includes discovery section with untested/new URL entries
- [ ] Unit tests for generator output shape

## Blocked by

- [10-playwright-invoker-ui-tier.md](./10-playwright-invoker-ui-tier.md)

## User stories covered

- 56, 57, 58, 59

## Notes

- v1 scope: HTML link extraction from pages visited during tests; full-site spider optional stretch.
- OpenAPI route inventory optional follow-up, not required for this slice.
