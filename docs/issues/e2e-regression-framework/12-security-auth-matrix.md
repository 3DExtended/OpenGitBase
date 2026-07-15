<!-- forge: #81 -->

# Curated security auth matrix

## Metadata

- ID: e2e-12
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-regression-framework.md

## Parent

[PRD: Unified E2E Regression Framework](../../prd/e2e-regression-framework.md)

## What to build

Add deterministic authorization and adversarial API tests with committed baselines:

1. **Table-driven matrix** for one protected resource (e.g. repository mutation or merge request action) across: anonymous, outsider, reader, writer, admin/owner.
2. **Curated malformed requests** — missing auth header, swapped resource id, invalid JSON body — with expected 401/403/404 (not 500).
3. Each case produces human intent transcript line + API baseline for status and normalized error body.
4. Extend API client to send raw malformed requests without throwing before capture.

Start with one feature domain; pattern reusable for others later.

## Acceptance criteria

- [ ] At least five role/malformation cases with committed baselines
- [ ] Anonymous and outsider cases assert correct 401/403/404 semantics
- [ ] Malformed payload returns expected error class, not 500
- [ ] Transcript readable as manual security checklist
- [ ] Baseline diff surfaces authorization regression in git review

## Blocked by

- [08-identity-seed-auth-journey.md](./08-identity-seed-auth-journey.md)

## User stories covered

- 51, 52

## Notes

- Parallel track with e2e-09/10 after e2e-08 — no dependency on git or Playwright slices.
- Fuzz tier (e2e-13) builds on patterns established here.
