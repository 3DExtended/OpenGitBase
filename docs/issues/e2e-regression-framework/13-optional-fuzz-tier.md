# Optional fuzz tier

## Metadata

- ID: e2e-13
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-regression-framework.md

## Parent

[PRD: Unified E2E Regression Framework](../../prd/e2e-regression-framework.md)

## What to build

Add optional adversarial fuzz execution via `--fuzz`:

1. **IFuzzRunner** — mutate valid request templates (strip auth, swap GUIDs, truncate fields, garbage types).
2. **Expected outcome catalog** — each scenario declares expected error class (401, 403, 404, 400).
3. **Failure rules** — wrong class fails run (e.g. 500 when 403 expected); unexpected 5xx always fails.
4. **Report section** — fuzz results appended to HTML in clearly marked non-baseline area.
5. **No committed baselines** for fuzz — report-only.

Fuzz tier runs last, after UI tier, only when `--fuzz` flag set. Default local regression skips fuzz.

## Acceptance criteria

- [ ] `--fuzz` adds final tier after standard tiers
- [ ] Default run without `--fuzz` skips fuzz tier entirely
- [ ] Fuzz report section distinct from baseline-gated tests
- [ ] Induced 500 when 403 expected fails the run
- [ ] At least three mutation scenarios demonstrated
- [ ] Fuzz does not write golden files

## Blocked by

- [12-security-auth-matrix.md](./12-security-auth-matrix.md)

## User stories covered

- 53, 54, 55

## Notes

- Not a substitute for curated matrix — complements it for exploratory signal.
- Keep mutations bounded to avoid destructive data corruption outside isolated test suffix namespace.
