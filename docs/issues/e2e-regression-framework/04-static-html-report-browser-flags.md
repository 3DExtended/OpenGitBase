<!-- forge: #73 -->

# Static HTML report + browser open flags

## Metadata

- ID: e2e-04
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-regression-framework.md

## Parent

[PRD: Unified E2E Regression Framework](../../prd/e2e-regression-framework.md)

## What to build

Build the static report generator and wire runner browser behavior:

1. **Self-contained HTML site** per run under local reports directory — no CDN/npm at view time; relative asset paths only.
2. **Index page** — run summary, per-test pass/fail, transcript rendering, baseline diff highlights from e2e-03.
3. **`latest` pointer** — copy or symlink so browser open target is stable.
4. **Browser flags** — open default browser on failure by default; `--open-report` opens on success too; `--no-open-report` disables.
5. Unit tests for HTML builder without compose.

Reports are **never committed** to git.

## Acceptance criteria

- [ ] Failed run produces browsable `index.html` with transcript and diff section
- [ ] All report assets use relative paths (offline-safe after copy/unzip)
- [ ] `latest` report path updated each run
- [ ] Browser opens automatically on failure; does not open on pass unless `--open-report`
- [ ] `--no-open-report` suppresses browser launch
- [ ] Report generator covered by unit tests

## Blocked by

- [03-baseline-manager-api-bundle.md](./03-baseline-manager-api-bundle.md)

## User stories covered

- 5, 6, 7, 8, 60, 61, 62, 63

## Notes

- Playwright embeds, git output panels, email viewers, and tier skip sections added incrementally in later slices.
- Do not integrate with GitHub or OpenGitBase web UI (out of scope per PRD).
