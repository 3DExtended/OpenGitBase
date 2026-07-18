# Code review remediation — implementation issues

Vertical slices from the project-wide code review (security, behavioral regressions, missing tests). Implement in dependency order; each issue is blocked by the ones listed in its file.

| # | ID | Issue | Type | Status | Blocked by |
|---|-----|-------|------|--------|------------|
| 1 | `sec-01` | [Production MSW and test artifact lockdown](./01-production-msw-lockdown.md) | AFK | ready | — |
| 2 | `sec-03` | [Repository access checks and DTO redaction](./03-repository-access-dto-redaction.md) | AFK | ready | — |
| 3 | `sec-05` | [Production secrets and compose profile separation](./05-production-secrets-compose.md) | AFK | ready | — |
| 4 | `fix-01` | [Commit page navigation and error parity](./07-commit-page-error-parity.md) | AFK | ready | — |
| 5 | `fix-02` | [MR page error handling and review threads](./08-mr-page-error-handling.md) | AFK | ready | — |
| 6 | `sec-02` | [Internal network trust behind reverse proxy](./02-internal-network-trust.md) | HITL | ready | — |
| 7 | `sec-04` | [Storage destructive ops and push enforcement](./04-storage-push-hardening.md) | AFK | ready | 6 |
| 8 | `sec-06` | [Web auth redirect and site gate policy](./06-web-auth-redirect-site-gate.md) | HITL | done | — |
| 9 | `fix-03` | [Commit change view test coverage gaps](./09-commit-view-test-gaps.md) | AFK | ready | 4 |

**Recommended order:** sec-01 → sec-03 → sec-05 → fix-01 → fix-02 → sec-02 → sec-04 → sec-06 → fix-03

## Dependency graph

```
sec-01, sec-03, sec-05, fix-01, fix-02, sec-06 ──► (independent)

sec-02 ──► sec-04

fix-01 ──► fix-03
```

## Source

- Code review conversation (project-wide review, Jul 2026)
