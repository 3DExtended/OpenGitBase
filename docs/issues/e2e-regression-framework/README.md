# Unified E2E Regression Framework — implementation issues

Vertical slices for [PRD: Unified E2E Regression Framework](../../prd/e2e-regression-framework.md).

Implement in dependency order; parallel tracks after **e2e-08** are OK (git HTTPS, Playwright, security matrix, discussions).

| # | ID | Issue | Type | Blocked by |
|---|-----|-------|------|------------|
| 1 | `e2e-01` | [Runner skeleton + fast compose + Tier 0 smoke](./01-runner-skeleton-fast-compose-tier0.md) | AFK | — |
| 2 | `e2e-02` | [Operation transcript + auto-logging API client](./02-operation-transcript-api-client.md) | AFK | 1 |
| 3 | `e2e-03` | [Baseline manager + update-baselines](./03-baseline-manager-api-bundle.md) | AFK | 2 |
| 4 | `e2e-04` | [Static HTML report + browser open flags](./04-static-html-report-browser-flags.md) | AFK | 3 |
| 5 | `e2e-05` | [Tier orchestrator + skip recording](./05-tier-orchestrator-skip-recording.md) | AFK | 4 |
| 6 | `e2e-06` | [Test isolation + baseline normalization](./06-test-isolation-db-reset-normalization.md) | AFK | 5 |
| 7 | `e2e-07` | [Capturing SendGrid sender + E2E mail API](./07-capturing-email-sender-e2e-api.md) | AFK | 6 |
| 8 | `e2e-08` | [Identity seed tier + auth journey test](./08-identity-seed-auth-journey.md) | AFK | 7 |
| 9 | `e2e-09` | [Git facade + HTTPS PAT scenario](./09-git-facade-https-pat-scenario.md) | AFK | 8 |
| 10 | `e2e-10` | [Playwright invoker + UI tier + report embed](./10-playwright-invoker-ui-tier.md) | AFK | 4, 8 |
| 11 | `e2e-11` | [URL discovery + Discovered skeleton generator](./11-url-discovery-skeleton-generator.md) | AFK | 10 |
| 12 | `e2e-12` | [Curated security auth matrix](./12-security-auth-matrix.md) | AFK | 8 |
| 13 | `e2e-13` | [Optional fuzz tier](./13-optional-fuzz-tier.md) | AFK | 12 |
| 14 | `e2e-14` | [Full-HA compose profile + chaos helpers](./14-full-ha-profile-chaos-helpers.md) | AFK | 1 |
| 15 | `e2e-15` | [HA storage chaos scenarios](./15-ha-storage-chaos-scenarios.md) | AFK | 9, 14 |
| 16 | `e2e-16` | [Merge request E2E scenarios](./16-merge-request-e2e-scenarios.md) | AFK | 9 |
| 17 | `e2e-18` | [Discussion E2E scenarios](./18-discussion-e2e-scenarios.md) | AFK | 7, 8 |
| 18 | `e2e-19` | [Repository browse E2E scenarios](./19-repository-browse-e2e-scenarios.md) | AFK | 9 |
| 19 | `e2e-20` | [Runner documentation + shell script retirement](./20-runner-docs-shell-retirement.md) | AFK | 9, 15, 16, 18, 19 |

Note: issue numbers **17–18** in filenames skip `e2e-17` to keep IDs aligned with slice IDs (`e2e-18`, `e2e-19`, `e2e-20`).
