# Commit change view test coverage gaps

## Metadata

- ID: fix-03
- Type: AFK
- Status: ready
- Source: code review (Jul 2026)

## What to build

Close test gaps identified in the commit change view PRD and code review that were not covered by cv-01–cv-07.

**Coverage to add:**

- Storage: ambiguous SHA prefix returns distinguishable error; merge commit returns first-parent diff (not combined merge diff).
- API: authenticated member on private repo → 200; root commit payload includes `kind: "root"` and `rootFiles`; prefix SHA resolves to canonical full SHA in response.
- Web: canonical URL `router.replace` when abbreviated SHA loads (assert URL bar updates).
- Compose: at least one end-to-end path beyond API-only `PublicCommitReadReturnsRootTreeForInitialCommit` — e.g. UI click-through on compose stack or documented compose smoke script.
- Optional: `RepoCommitLink` href and `from` query unit tests.

## Acceptance criteria

- [ ] Storage test: ambiguous prefix → error; merge commit → first-parent hunks
- [ ] API test: private repo member authorized → 200 with commit payload
- [ ] API test: root commit response shape includes root file listing
- [ ] API test: prefix SHA in request returns full canonical SHA
- [ ] Playwright or Vitest: abbreviated SHA in URL replaced with full SHA after load
- [ ] Compose E2E or documented smoke: commit read verifiable on running stack (extend existing BrowseE2e or add UI step)
- [ ] All new tests pass in CI

## Blocked by

- [Commit page navigation and error parity](./07-commit-page-error-parity.md) — canonical URL assertion depends on stable commit page load behavior

## Findings covered

- Medium: PRD-required test coverage largely missing (ambiguous SHA, merge commit, API auth matrix, root payload, canonical URL, compose UI E2E)
- Low: no `RepoCommitLink` unit tests

## Notes

Parent feature issues: [commit-change-view](../commit-change-view/README.md) (cv-01–cv-07 completed). This slice is follow-up hardening, not a repeat of core feature delivery.
