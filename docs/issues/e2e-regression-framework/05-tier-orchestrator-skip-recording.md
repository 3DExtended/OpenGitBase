# Tier orchestrator + skip recording

## Metadata

- ID: e2e-05
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-regression-framework.md

## Parent

[PRD: Unified E2E Regression Framework](../../prd/e2e-regression-framework.md)

## What to build

Implement tier-based test execution in the runner:

| Tier | Behavior on failure |
|------|---------------------|
| 0 Infrastructure | Fail-fast; skip all higher tiers |
| 1 Auth (placeholder until e2e-08) | Fail-fast; skip all higher tiers |
| 2+ Feature | Run all tests in tier; skip higher tiers if tier failed |

Record skipped tiers and reasons in run result for the HTML report.

Tests declare tier via attribute or collection convention. Demonstrate with: Tier 0 pass, Tier 2 containing one pass + one fail (second test still runs), Tier UI skipped with reason in report.

## Acceptance criteria

- [ ] Tier 0 fail-fast prevents later tiers from executing
- [ ] Feature tier runs all tests even after individual failure within tier
- [ ] Later tiers skipped when earlier tier fails; skip reason in report
- [ ] Report shows per-tier pass/fail/skip summary
- [ ] `--filter` restricts which tests/tiers run (basic expression sufficient for v1)

## Blocked by

- [04-static-html-report-browser-flags.md](./04-static-html-report-browser-flags.md)

## User stories covered

- 9, 37, 38, 39, 40, 41

## Notes

- UI and Fuzz tier slots reserved; wired in e2e-10 and e2e-13.
- Tier 1 populated with real auth tests in e2e-08.
