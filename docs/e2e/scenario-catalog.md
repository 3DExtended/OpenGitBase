# E2E scenario catalog

Living index for [E2E test population](../../prd/e2e-test-population.md). Update **in the same PR** as new scenarios.

## ID format

`E2E-<Feature>-<nnn>` — Feature codes: F00 (infra), F01 (auth), F02 (org), F03 (repo settings), F04 (members), F05 (browse), F06 (discussion), F07 (MR), F08 (git HTTPS), F09 (SSH), F10 (HA), F11 (admin), F12 (discovery), SEC (security cross-cut), FUZZ (fuzz tier).

## Status values

`pending` | `in-progress` | `done`

## Template row

| ID | Feature | Scenario | Tags | PRD stories | Parity | Status | Owner |
|----|---------|----------|------|-------------|--------|--------|-------|
| E2E-Fxx-nnn | Fxx Name | `ClassName.MethodName` | Smoke, Regression | 26 | — | pending | |

## Catalog

| ID | Feature | Scenario | Tags | PRD stories | Parity | Status | Owner |
|----|---------|----------|------|-------------|--------|--------|-------|
| E2E-F00-001 | F00 Infrastructure | `InfrastructureSmokeTests.ApiHealthReturnsHealthy` | Smoke, Regression | 37 | e2e-01 | done | — |
| E2E-F01-001 | F01 Auth | `IdentitySeedTests.SeedCoreRolesCreatesUsers` | Smoke, Regression | 27, 44 | e2e-08 | done | — |
| E2E-F01-002 | F01 Auth | `AuthJourneyTests.RegisterCapturedEmailVerifyAndLogin` | Smoke, Regression | 26, 45 | e2e-08 | done | — |
| E2E-F08-001 | F08 Git HTTPS | `PatPushCloneTests.PatPushCloneAndReadScopeDenial` | Smoke, Regression | 30, 87 | e2e-09 | done | — |
| E2E-SEC-001 | Security | `AuthMatrixTests.AnonymousCannotMutate` | Smoke, Regression | 51 | e2e-12 | done | — |
| E2E-F06-001 | F06 Discussion | `DiscussionE2eTests.PublicAnonymousReadAndCreateForbidden` | Smoke, Regression | 65 | disc-10 | done | — |
| E2E-F06-002 | F06 Discussion | `DiscussionE2eTests.PrivateRepoAccessMatrix` | Smoke, Regression | 66 | disc-10 | done | — |
| E2E-F06-003 | F06 Discussion | `DiscussionE2eTests.DiscussionCommentLifecycle` | Smoke, Regression | 67 | disc-10 | done | — |
| E2E-F05-001 | F05 Browse | `BrowseE2eTests.PublicRefsAndTreeAnonymousAccess` | Smoke, Regression | 56 | repo-browse-11 | done | — |
| E2E-F07-001 | F07 Merge request | `MergeRequestE2eTests.ProtectBranchPushDeniedAndCreateMergeRequest` | Smoke, Regression | 76 | mr-16 | done | — |
| E2E-F10-001 | F10 HA storage | `HaStorageChaosTests.StopStorageNodeStillAllowsHealthCheck` | Smoke, Regression, FullHa | 105 | ha-storage-12 | done | — |
| E2E-FUZZ-001 | Fuzz | `FuzzIntegrationTests.MutatedAnonymousRequestReturnsExpectedErrorClass` | Regression | 53 | e2e-13 | done | — |

## Coverage summary (target)

| Feature | Done | Target smoke | Target regression |
|---------|-----:|-------------:|------------------:|
| F01 Auth | 2 | 10 | 55 |
| F02 Organizations | 0 | 10 | 52 |
| F03 Repository settings | 0 | 10 | 54 |
| F04 Members | 0 | 10 | 50 |
| F05 Browse | 1 | 10 | 52 |
| F06 Discussion | 3 | 10 | 58 |
| F07 Merge requests | 1 | 10 | 56 |
| F08 Git HTTPS | 1 | 10 | 54 |
| F09 SSH git | 0 | 10 | 50 |
| F10 HA storage | 1 | 10 | 52 |
| F11 Admin fleet | 0 | 10 | 50 |
| F12 Discovery | 0 | 10 | 50 |

Matrix-backed scenarios use catalog IDs `E2E-POP##-###` in test case records; see `*RegressionMatrixTests` and `*SmokeTests` classes.

Pending rows are added by work items pop-09 … pop-28.
