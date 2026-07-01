# PRD issues TDD (main) — E2E Test Population

Source: [docs/prd/e2e-test-population.md](../../../docs/prd/e2e-test-population.md)  
Work items: [docs/issues/e2e-test-population/README.md](../../../docs/issues/e2e-test-population/README.md)

Branch strategy: **main** (all work items committed sequentially on default branch).

**Status:** in progress

## Execution order

| Order | ID | Title | Status | Blocked by |
|------:|-----|-------|--------|------------|
| 1 | pop-01 | Scenario catalog + authoring checklist | done | — |
| 2 | pop-02 | Shared fixture library | done | — |
| 3 | pop-05 | Runner tag and feature filters | done | — |
| 4 | pop-08 | Full-HA tier gating | done | — |
| 5 | pop-03 | Git testdata provisioning | in_progress | pop-02 |
| 6 | pop-04 | Auth matrix theory runner | pending | pop-02 |
| 7 | pop-06 | Report feature rollup | pending | pop-01 |
| 8 | pop-07 | Integration test promotion indexer | pending | pop-01 |
| 9 | pop-09 | F05 browse parity smoke | pending | pop-02, pop-03 |
| 10 | pop-10 | F07 MR parity smoke | pending | pop-02, pop-03 |
| 11 | pop-11 | F06 discussion parity smoke | pending | pop-02 |
| 12 | pop-12 | F08 git HTTPS smoke expansion | pending | pop-02 |
| 13 | pop-13 | F10 HA parity smoke | pending | pop-02, pop-08 |
| 14 | pop-14 | F01 auth smoke pack | pending | pop-02, pop-04 |
| 15–30 | pop-15 … pop-30 | Smoke/regression/UI/CI slices | pending | see issues README |

## Entry point

```bash
dotnet run --project tests/OpenGitBase.E2E.Runner
```

See [tests/OpenGitBase.E2E/README.md](../../../tests/OpenGitBase.E2E/README.md).
