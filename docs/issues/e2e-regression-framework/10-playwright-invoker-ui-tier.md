# Playwright invoker + UI tier + report embed

## Metadata

- ID: e2e-10
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-regression-framework.md

## Parent

[PRD: Unified E2E Regression Framework](../../prd/e2e-regression-framework.md)

## What to build

Integrate existing TypeScript Playwright specs into the unified runner:

1. **Playwright subprocess invoker** — run `npx playwright test` from web app test directory with tag filter.
2. **Tag convention** — only specs marked `@regression` (or equivalent project filter) run in default suite.
3. **UI tier** — executes after Tier 1 auth succeeds; skipped if Tier 1 failed.
4. **Artifact collection** — copy Playwright HTML report, screenshots, traces into unified static report (not into committed baselines).
5. Tag at least one existing visual spec for regression inclusion as proof.

Do **not** migrate specs to Playwright for .NET.

## Acceptance criteria

- [ ] Runner invokes TS Playwright with regression tag filter
- [ ] UI tier skipped when auth tier fails; reason in report
- [ ] Playwright screenshots/traces visible in unified HTML report
- [ ] Playwright pixels are not written to committed baseline bundles
- [ ] At least one `@regression` spec runs green in full suite
- [ ] `--filter` can target UI tier only

## Blocked by

- [04-static-html-report-browser-flags.md](./04-static-html-report-browser-flags.md)
- [08-identity-seed-auth-journey.md](./08-identity-seed-auth-journey.md)

## User stories covered

- 23, 24, 25, 26

## Notes

- Blocks URL discovery (e2e-11) which extracts links from visited pages.
- Existing Playwright config and visual specs remain in web application project.
