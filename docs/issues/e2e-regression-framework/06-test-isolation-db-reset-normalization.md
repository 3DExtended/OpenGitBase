<!-- forge: #75 -->

# Test isolation + baseline normalization

## Metadata

- ID: e2e-06
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-regression-framework.md

## Parent

[PRD: Unified E2E Regression Framework](../../prd/e2e-regression-framework.md)

## What to build

Ensure tests do not leak state and baselines stay stable:

1. **Per-test unique suffix** (`RunSuffix`) applied to usernames, repo slugs, and other entity names.
2. **Database reset between tests** — truncate relevant tables or equivalent clean slate before each test method.
3. **Baseline normalizer** — replace volatile values with placeholders (`{{RUN_SUFFIX}}`, `{{USER_ID}}`, timestamps, JWT fragments) before diff/update.
4. Demonstration: two tests in same tier create repos with same logical name pattern but different suffixes; both pass without collision; committed baselines use placeholders not raw UUIDs.

## Acceptance criteria

- [ ] Each test method receives unique run suffix
- [ ] DB reset runs between tests; no cross-test pollution in feature tier demo
- [ ] Baseline diff stable across two consecutive green runs with different suffixes
- [ ] Normalizer unit tests cover token replacement rules
- [ ] Parallel-safe naming convention documented for test authors

## Blocked by

- [05-tier-orchestrator-skip-recording.md](./05-tier-orchestrator-skip-recording.md)

## User stories covered

- 16, 42, 43

## Notes

- Reset strategy should be fast enough for local iteration; prefer truncate over full compose restart.
- Identity seeder (e2e-08) builds on this fixture.
