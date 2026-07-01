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
