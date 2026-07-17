# E2E scenario catalog

Living index for [E2E test population](../../prd/e2e-test-population.md). Update **in the same PR** as new scenarios.

_Reconciled 2026-07-17: Done counts include smoke/e2e facts **and** auth-matrix/`InlineData`/`MemberData` theory rows from `*RegressionMatrix` / `*SmokeTests` classes. Query-param filler pads removed._

## ID format

`E2E-<Feature>-<nnn>` — Feature codes: F00 (infra), F01 (auth), F02 (org), F03 (repo settings), F04 (members), F05 (browse), F06 (discussion), F07 (MR), F08 (git HTTPS), F09 (SSH), F10 (HA), F11 (admin), F12 (discovery), SEC (security cross-cut), FUZZ (fuzz tier).

Matrix packs also use `E2E-POP##-###` IDs inside `BuildCases()`; each theory data row is one catalog scenario.

## Status values

`pending` | `in-progress` | `done`

## Coverage summary (target)

| Feature | Done | Target smoke | Target regression | Primary sources |
|---------|-----:|-------------:|------------------:|-----------------|
| F01 Auth | 60 | 10 | 55 | `AuthSmokeTests (8+2), AuthJourneyTests, IdentitySeedTests, AuthRegressionMatrix` |
| F02 Organizations | 64 | 10 | 52 | `OrganizationSmokeTests (8), OrganizationRegressionMatrix` |
| F03 Repository settings | 68 | 10 | 54 | `RepositorySettingsSmokeTests (10), RepositorySettingsRegressionMatrix` |
| F04 Members | 60 | 10 | 50 | `RepositoryMemberSmokeTests (10), RepositoryMemberMatrix` |
| F05 Browse | 66 | 10 | 52 | `BrowseSmokeTests (5), BrowseE2eTests (3), BrowseRegressionMatrix` |
| F06 Discussion | 75 | 10 | 58 | `DiscussionSmokeTests (6), DiscussionE2eTests (3), DiscussionRegressionMatrix` |
| F07 Merge requests | 69 | 10 | 56 | `MergeRequestSmokeTests (5), MergeRequestE2eTests (+links), MergeRequestRegressionMatrix` |
| F08 Git HTTPS | 57 | 10 | 54 | `GitHttpsSmokeTests (6), PatPushCloneTests, GitHttpsRegressionMatrix` |
| F09 SSH git | 56 | 10 | 50 | `GitSshRegressionMatrix (key CRUD + gated transport)` |
| F10 HA storage | 56 | 10 | 52 | `HaSmokeTests (3), HaStorageChaosTests, HaRegressionMatrix (@FullHa)` |
| F11 Admin fleet | 76 | 10 | 50 | `AdminFleetSmokeTests (10), AdminFleetRegressionMatrix` |
| F12 Discovery | 64 | 10 | 50 | `DiscoverySmokeTests (10), DiscoveryRegressionMatrix` |

**Total (F01–F12):** 781 compose scenarios (facts + theory rows). Cross-cutting SEC/FUZZ/F00 counted separately in catalog rows below.

## Catalog (index rows)

| ID | Feature | Scenario | Tags | PRD stories | Parity | Status | Owner |
|----|---------|----------|------|-------------|--------|--------|-------|
| E2E-F00-001 | F00 Infrastructure | `InfrastructureSmokeTests.ApiHealthReturnsHealthy` | Smoke, Regression | 37 | e2e-01 | done | — |
| E2E-F00-002 | F00 Infrastructure | `PublicStatusSmokeTests.PublicStatusReturnsSnapshotShape` | Smoke | — | — | done | — |
| E2E-F00-003 | F00 Infrastructure | `PublicStatusSmokeTests.PublicStatusHistoryReturnsSeriesShape` | Smoke | — | — | done | — |
| E2E-F01-001 | F01 Auth | `IdentitySeedTests.SeedCoreRolesCreatesUsers` | Smoke, Regression | 27, 44 | e2e-08 | done | — |
| E2E-F01-002 | F01 Auth | `AuthJourneyTests.RegisterCapturedEmailVerifyAndLogin` | Smoke, Regression | 26, 45 | e2e-08 | done | — |
| E2E-F01-S01 | F01 Auth | `AuthSmokeTests (8 facts + 2 anonymous theory rows)` | Smoke | 28–36 | pop-14 | done | — |
| E2E-POP15 | F01 Auth | `AuthRegressionMatrix.BuildCases (48 theory rows)` | Regression | 26–37 | pop-15 | done | — |
| E2E-F02-S01 | F02 Organizations | `OrganizationSmokeTests (8 facts)` | Smoke | 38–44 | pop-16 | done | — |
| E2E-POP17 | F02 Organizations | `OrganizationRegressionMatrix.BuildCases (56 theory rows)` | Regression | 38–44 | pop-17 | done | — |
| E2E-F03-S01 | F03 Repository settings | `RepositorySettingsSmokeTests (10 facts)` | Smoke | 45–51 | pop-18 | done | — |
| E2E-POP19 | F03 Repository settings | `RepositorySettingsRegressionMatrix.BuildCases (58 theory rows)` | Regression | 45–51 | pop-19 | done | — |
| E2E-F04-S01 | F04 Members | `RepositoryMemberSmokeTests (10 facts)` | Smoke | 52–55 | pop-16 | done | — |
| E2E-F04-M | F04 Members | `RepositoryMemberMatrix.BuildCases (50 theory rows)` | Smoke, Regression | 52–55 | pop-17 | done | — |
| E2E-F05-001 | F05 Browse | `BrowseE2eTests.PublicRefsAndTreeAnonymousAccess` | Smoke, Regression | 56 | repo-browse-11 | done | — |
| E2E-F05-S01 | F05 Browse | `BrowseSmokeTests (5 facts) + BrowseE2eTests extras` | Smoke | 56–64 | pop-09 | done | — |
| E2E-POP20 | F05 Browse | `BrowseRegressionMatrix.BuildCases (58 theory rows)` | Regression | 56–64 | pop-20 | done | — |
| E2E-F06-001 | F06 Discussion | `DiscussionE2eTests.PublicAnonymousReadAndCreateForbidden` | Smoke, Regression | 65 | disc-10 | done | — |
| E2E-F06-002 | F06 Discussion | `DiscussionE2eTests.PrivateRepoAccessMatrix` | Smoke, Regression | 66 | disc-10 | done | — |
| E2E-F06-003 | F06 Discussion | `DiscussionE2eTests.DiscussionCommentLifecycle` | Smoke, Regression | 67 | disc-10 | done | — |
| E2E-F06-S01 | F06 Discussion | `DiscussionSmokeTests (6 facts)` | Smoke | 65–75 | pop-11 | done | — |
| E2E-POP21 | F06 Discussion | `DiscussionRegressionMatrix.BuildCases (66 theory rows)` | Regression | 65–75 | pop-21 | done | — |
| E2E-F07-001 | F07 Merge request | `MergeRequestE2eTests.ProtectBranchPushDeniedAndCreateMergeRequest` | Smoke, Regression | 76 | mr-16 | done | — |
| E2E-F07-S01 | F07 Merge request | `MergeRequestSmokeTests (5) + discussion-links E2e` | Smoke | 76–86 | pop-10 | done | — |
| E2E-POP22 | F07 Merge request | `MergeRequestRegressionMatrix.BuildCases (62 theory rows)` | Regression | 76–86 | pop-22 | done | — |
| E2E-F08-001 | F08 Git HTTPS | `PatPushCloneTests.PatPushCloneAndReadScopeDenial` | Smoke, Regression | 30, 87 | e2e-09 | done | — |
| E2E-F08-S01 | F08 Git HTTPS | `GitHttpsSmokeTests (6 facts)` | Smoke | 87–94 | pop-12 | done | — |
| E2E-POP23 | F08 Git HTTPS | `GitHttpsRegressionMatrix.BuildCases (50 theory rows)` | Regression | 87–94 | pop-23 | done | — |
| E2E-POP28 | F09 SSH git | `GitSshRegressionMatrix.BuildCases (56 theory rows; transport gated)` | Regression | 95–98 | pop-28 | done | — |
| E2E-F10-001 | F10 HA storage | `HaStorageChaosTests.StopStorageNodeStillAllowsHealthCheck` | Smoke, Regression, FullHa | 105 | ha-storage-12 | done | — |
| E2E-F10-S01 | F10 HA storage | `HaSmokeTests (3 FullHa chaos facts)` | Smoke, FullHa | 99–106 | pop-13 | done | — |
| E2E-POP26 | F10 HA storage | `HaRegressionMatrix.BuildCases (52 theory rows; @FullHa)` | Regression, FullHa | 99–106 | pop-26 | done | — |
| E2E-F11-S01 | F11 Admin fleet | `AdminFleetSmokeTests (10 facts)` | Smoke | 107–110 | pop-27 | done | — |
| E2E-POP27 | F11 Admin fleet | `AdminFleetRegressionMatrix.BuildCases (66 theory rows)` | Regression | 107–110 | pop-27 | done | — |
| E2E-F12-S01 | F12 Discovery | `DiscoverySmokeTests (10 facts)` | Smoke | 111–114 | pop-24 | done | — |
| E2E-POP25 | F12 Discovery | `DiscoveryRegressionMatrix.BuildCases (54 theory rows)` | Regression | 111–114 | pop-25 | done | — |
| E2E-SEC-001 | Security | `AuthMatrixTests.AnonymousCannotMutate` | Smoke, Regression | 51 | e2e-12 | done | — |
| E2E-FUZZ-001 | Fuzz | `FuzzIntegrationTests.MutatedAnonymousRequestReturnsExpectedErrorClass` | Regression | 53 | e2e-13 | done | — |

### Matrix expansion note

Individual theory rows are authored in:

- `tests/OpenGitBase.E2E.Tests/**/*RegressionMatrixTests.cs`
- `tests/OpenGitBase.E2E.Tests/Repository/RepositoryMemberAuthMatrixTests.cs`
- `tests/OpenGitBase.E2E.Tests/Auth/AuthSmokeTests.cs` (anonymous matrix rows)

Unit guard: `MatrixCoverageGuardTests` asserts each domain matrix `BuildCases().Count >= 50` (F01 floor 48 + smoke/journey closes the PRD target).

F09 transport scenarios and F10 chaos matrix rows are `@FullHa` / profile-gated (`NotApplicable` when profile inactive); key CRUD and admin auth matrices still run on fast compose.

