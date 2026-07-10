# ers-15 handoff

- PRD: `docs/prd/encrypted-replica-storage.md` (Capacity Placement Engine, Phase 3)
- Work item: **ers-15** — Cross-org encrypted placement algorithm
- Branch: `main`

## Acceptance criteria

- Encrypted placement prefers foreign-org `CrossOrgAllowed` nodes
- Same-org nodes never used for plaintext primary/read on foreign nodes
- Platform fallback when community capacity exhausted
- Skip nodes exceeding MaxBytes for repo size estimate
- Planner unit tests: cross-org preference, fallback, same-org exclusion
- Integration test: encrypted copies on second org's node (two-org setup)

## Dependencies

- Direct: ers-13, ers-14
- Chain: ers-11 → ers-12 → ers-13/14 → **ers-15**
