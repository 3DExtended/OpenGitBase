# E2E Test Population — implementation issues

Vertical slices for [PRD: E2E Test Population](../../prd/e2e-test-population.md).

Target: **~630 `@Regression` scenarios** (~50+ per feature domain) and **~120 `@Smoke`** scenarios across 12 product domains. Builds on the [unified E2E framework](../e2e-regression-framework/README.md) (already on `main`).

Implement in dependency order. Parallel tracks after **pop-02** are OK (runner filters, HA tier gating, catalog).

| # | ID | Issue | Type | Blocked by |
|---|-----|-------|------|------------|
| 1 | `pop-01` | [Scenario catalog + authoring checklist](./01-scenario-catalog-authoring-checklist.md) | AFK | — |
| 2 | `pop-02` | [Shared fixture library](./02-shared-fixture-library.md) | AFK | — |
| 3 | `pop-03` | [Git testdata provisioning](./03-git-testdata-provisioning.md) | AFK | 2 |
| 4 | `pop-04` | [Auth matrix theory runner](./04-auth-matrix-theory-runner.md) | AFK | 2 |
| 5 | `pop-05` | [Runner tag and feature filters](./05-runner-tag-feature-filters.md) | AFK | — |
| 6 | `pop-06` | [Report feature rollup dashboard](./06-report-feature-rollup.md) | AFK | 1 |
| 7 | `pop-07` | [Integration test promotion indexer](./07-integration-test-promotion-indexer.md) | AFK | 1 |
| 8 | `pop-08` | [Full-HA tier gating](./08-full-ha-tier-gating.md) | AFK | — |
| 9 | `pop-09` | [F05 browse parity smoke](./09-f05-browse-parity-smoke.md) | AFK | 2, 3 |
| 10 | `pop-10` | [F07 merge request parity smoke](./10-f07-merge-request-parity-smoke.md) | AFK | 2, 3 |
| 11 | `pop-11` | [F06 discussion parity smoke](./11-f06-discussion-parity-smoke.md) | AFK | 2 |
| 12 | `pop-12` | [F08 git HTTPS smoke expansion](./12-f08-git-https-smoke-expansion.md) | AFK | 2 |
| 13 | `pop-13` | [F10 HA parity smoke](./13-f10-ha-parity-smoke.md) | AFK | 2, 8 |
| 14 | `pop-14` | [F01 auth smoke pack](./14-f01-auth-smoke-pack.md) | AFK | 2, 4 |
| 15 | `pop-15` | [F01 auth regression matrix](./15-f01-auth-regression-matrix.md) | AFK | 14, 4 |
| 16 | `pop-16` | [F02 org + F04 members smoke](./16-f02-org-f04-members-smoke.md) | AFK | 2, 4 |
| 17 | `pop-17` | [F02 org + F04 members regression](./17-f02-org-f04-members-regression.md) | AFK | 16, 4 |
| 18 | `pop-18` | [F03 repository settings smoke](./18-f03-repository-settings-smoke.md) | AFK | 2 |
| 19 | `pop-19` | [F03 repository settings regression](./19-f03-repository-settings-regression.md) | AFK | 18, 4 |
| 20 | `pop-20` | [F05 browse regression matrix](./20-f05-browse-regression-matrix.md) | AFK | 9, 4 |
| 21 | `pop-21` | [F06 discussion regression](./21-f06-discussion-regression.md) | AFK | 11, 4 |
| 22 | `pop-22` | [F07 merge request regression](./22-f07-merge-request-regression.md) | AFK | 10, 4 |
| 23 | `pop-23` | [F08 git HTTPS regression](./23-f08-git-https-regression.md) | AFK | 12, 4 |
| 24 | `pop-24` | [F12 discovery + notifications smoke](./24-f12-discovery-notifications-smoke.md) | AFK | 2 |
| 25 | `pop-25` | [F12 discovery + notifications regression](./25-f12-discovery-notifications-regression.md) | AFK | 24, 4 |
| 26 | `pop-26` | [F10 HA full regression](./26-f10-ha-full-regression.md) | AFK | 13, 8 |
| 27 | `pop-27` | [F11 admin fleet smoke + regression](./27-f11-admin-fleet-scenarios.md) | AFK | 2, 26 |
| 28 | `pop-28` | [F09 SSH git profile scenarios](./28-f09-ssh-git-profile-scenarios.md) | AFK | 2, 23 |
| 29 | `pop-29` | [Playwright behavioral regression specs](./29-playwright-behavioral-specs.md) | AFK | 1 |
| 30 | `pop-30` | [CI smoke vs regression documentation](./30-ci-smoke-regression-docs.md) | HITL | 5 |

## Waves

| Wave | Issues | Outcome |
|------|--------|---------|
| 0 — Infrastructure | pop-01 … pop-08 | Fixtures, catalog, filters, matrix runner |
| 1 — Shell parity | pop-09 … pop-13 | Close mr-16, disc-10, repo-browse-11, ha-storage-12 gaps |
| 2 — Smoke packs | pop-14 … pop-24 | ~10 `@Smoke` scenarios per domain |
| 3 — Regression depth | pop-15 … pop-25, pop-26 … pop-28 | ~50+ `@Regression` per domain |
| 4 — UI & CI | pop-29, pop-30 | Playwright behavioral + CI strategy sign-off |
