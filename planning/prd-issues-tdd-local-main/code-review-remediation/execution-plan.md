# Code review remediation — execution plan

Branch strategy: **main** (all work items committed sequentially on default branch).

Source: `docs/issues/code-review-remediation/README.md`

## Work items (topological order)

| Order | ID | Title | Type | Status | Blocked by |
|-------|-----|-------|------|--------|------------|
| 1 | sec-01 | Production MSW and test artifact lockdown | AFK | pending | — |
| 2 | sec-03 | Repository access checks and DTO redaction | AFK | pending | — |
| 3 | sec-05 | Production secrets and compose profile separation | AFK | pending | — |
| 4 | fix-01 | Commit page navigation and error parity | AFK | pending | — |
| 5 | fix-02 | MR page error handling and review threads | AFK | pending | — |
| 6 | sec-02 | Internal network trust behind reverse proxy | HITL | pending | — |
| 7 | sec-04 | Storage destructive ops and push enforcement | AFK | pending | sec-02 |
| 8 | sec-06 | Web auth redirect and site gate policy | HITL | pending | — |
| 9 | fix-03 | Commit change view test coverage gaps | AFK | pending | fix-01 |

## Dependency graph

```
sec-01, sec-03, sec-05, fix-01, fix-02, sec-06 ──► independent
sec-02 ──► sec-04
fix-01 ──► fix-03
```

## HITL decisions (documented inline)

- **sec-02:** Trusted forwarded headers with explicit proxy allowlist; supplement with path restrictions for E2E endpoints.
- **sec-06:** Same-origin redirect validation; site gate disabled in production builds (`import.meta.dev` only).
