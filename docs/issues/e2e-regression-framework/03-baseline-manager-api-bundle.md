# Baseline manager + update-baselines

## Metadata

- ID: e2e-03
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-regression-framework.md

## Parent

[PRD: Unified E2E Regression Framework](../../prd/e2e-regression-framework.md)

## What to build

Implement committed behavioral baselines for API captures:

1. **IBaselineContext** — capture API response (status + normalized JSON body) and normalized transcript.
2. **Mirror layout** — baselines stored under path mirroring test logical location (e.g. smoke test folder with `operations.json`, `api/{step-id}.json`).
3. **Diff mode** — compare run artifacts to committed goldens; fail test on any normalized diff.
4. **Update mode** — `--update-baselines` writes or refreshes golden files.
5. **Missing baseline** — fail with actionable message pointing to update command.

One vertical smoke test: API call → capture → assert matches committed baseline (or update when flag set).

## Acceptance criteria

- [ ] Baseline bundle structure includes `operations.json` and at least one `api/*.json` artifact
- [ ] Baseline path mirrors test logical path
- [ ] Normal run fails when committed baseline missing
- [ ] `--update-baselines` creates/updates golden files in repo tree
- [ ] Normalized diff detects intentional JSON field change
- [ ] Unit tests for normalizer and diff engine with fixture golden directories

## Blocked by

- [02-operation-transcript-api-client.md](./02-operation-transcript-api-client.md)

## User stories covered

- 14, 15, 17, 18, 19

## Notes

- HTML, git, and side-channel baseline buckets added in later scenario slices; design interfaces to extend cleanly.
- Normalization tokens (`{{RUN_SUFFIX}}`, etc.) may be minimal here; full isolation rules land in e2e-06.
