# E2E test population — progress log

Branch: `main`

## 2026-07-01 — Run started

Executing [docs/issues/e2e-test-population/](../../../docs/issues/e2e-test-population/) on `main` per `/prd-issues-tdd-local-main`.

## Wave 0 (pop-01 … pop-08)

- pop-01: `24bdbbf` — scenario catalog
- pop-05: `aac93f0` — runner tag/feature filters
- pop-08: `dd6072a` — full-HA tier gating
- pop-02: `717b458` — fixture library
- pop-03: `1caf1b6` — git testdata fixture
- pop-04: `e896f7e` — auth matrix runner
- pop-06: `fa5ddee` — report feature rollup
- pop-07: `c9d045b` — promotion indexer

## Waves 1–4 (pop-09 … pop-30)

Committed in subsequent commits on `main` — smoke packs, regression matrix classes, Playwright behavioral specs, CI strategy doc.

**Note:** Compose-backed scenario baselines require stack + `--update-baselines` before full green compose run.

## 2026-07-17 — Gap reconciliation (PRD #12)

### Audit finding

Execution plan marked pop-01…30 “done”, but `docs/e2e/scenario-catalog.md` still showed most domains at 0–3. Matrices existed under `*RegressionMatrixTests` / `*SmokeTests` but were not indexed. Several matrices also padded with query-param / NotApplicable filler (`while id <= 60`, `members?probe=`, SSH/HA probe loops, GitHttps NotApplicable stubs).

### Gap report (before → after this session)

| Domain | Meaningful before | Target | After (facts + matrix) | Notes |
|--------|------------------:|-------:|-----------------------:|-------|
| F01 Auth | ~41 + filler | 55 | ~60 | Removed `account/me?catalog=` pad; added invite/reset edges |
| F02 Org | ~50 + filler | 52 | ~64 | Removed members?probe pad; added invite/storage edges |
| F03 Settings | ~68 | 54 | ~68 | Already at target |
| F04 Members | ~20 | 50 | 60 (10 smoke + 50 matrix) | Rewrote matrix; shared fixture + CloneAuth |
| F05–F08, F12 | at/above | … | at/above | Catalog reconciled |
| F09 SSH | ~5 + filler | 50 | 56 | Real key CRUD matrix + gated transport intents |
| F10 HA | ~9 + filler | 52 | 56 | Auth/fleet matrix + FullHa-gated rows; chaos facts remain |
| F11 Admin | ~28 | 50 | 76 (10 smoke + 66 matrix) | Platform admin login + AdminFleetSmokeTests |
| F12 Discovery | ~52 w/ probe dupes | 50 | ~64 | Removed `?probe=` filler |

### Compose verification (this session)

- `AdminFleetSmokeTests` — 10/10 green (`OPENGITBASE_E2E_UPDATE_BASELINES=1`)
- `AdminFleetRegressionMatrixTests` + `GitSshRegressionMatrixTests` — 122/122 green
- `RepositoryMemberAuthMatrixTests` — 50/50 green
- Unit `Category=E2EUnit` including `MatrixCoverageGuardTests` — green
- Full suite not re-run end-to-end (API/storage previously OOM’d under per-theory user storms); prefer shared fixtures going forward

### Still open before closing forge #12

- [x] Re-run Auth/Org matrices after filler strip on healthy stack — **104/104** (`2144e30`)
- [x] Comment + resolve pop forge discussions #89–#118 — **30/30**
- [x] Resolve #12 with summary linking catalog coverage

## 2026-07-17 — Closed

Forge **#12** resolved. Pop slices **#89–#118** resolved. Catalog + compose representative filters green on `main` (`c4e52d4`, `2144e30`).
