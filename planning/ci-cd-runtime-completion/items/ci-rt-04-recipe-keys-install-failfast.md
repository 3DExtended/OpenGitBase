# PRD recipe keys + install fail-fast

## Metadata

- ID: ci-rt-04
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-runtime-completion.md

## Parent

[PRD: CI/CD Runtime Completion](../../../docs/prd/ci-cd-runtime-completion.md)

## What to build

Replace dependency name-only recipe keys with the PRD formula: `sha256(normalize(baseSlug) + normalize(installscript))` consistently in the agent, telemetry recording, promotion eligibility queries, and admin analytics.

When a live `installscript` exits non-zero, mark the **Job** `Failed` immediately, skip the `script` phase, and apply normal stage gating. Preserve structured install-section logs distinguishing layer cache hit vs live install.

Fix promotion streak queries to order by recency before evaluating the last five outcomes.

## Acceptance criteria

- [ ] Recipe key computation matches PRD formula in agent and control plane
- [ ] Contract tests: same base+script → same key; whitespace normalization documented and tested
- [ ] Failed `installscript` → job `Failed`; `script` does not run
- [ ] Promotion eligibility uses last five installs in chronological order
- [ ] Install logs still show promoted layer cache hit vs live install

## Blocked by

- None — can start immediately

## User stories covered

- 27 — Content-addressed recipe keys
- 28 — Failed installscript fails the job immediately
- 29 — Logs show layer hit vs live install
- 32 — Promotion blocked unless last five installs succeeded (streak query correctness)

## Notes

- Implementable on process sandbox path before Firecracker slices land.
- Real overlay promotion artifacts are ci-rt-11.
