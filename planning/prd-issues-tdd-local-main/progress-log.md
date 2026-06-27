# PRD issues TDD (main) — progress log

## Run summary

| Order | ID | Title | Status | Commit |
|------:|-----|-------|--------|--------|
| 1 | mr-01 | Merge request authorization | completed | 4737099 |
| 2 | mr-02 | Default branch persistence | completed | 79a86ed |
| 3 | mr-03 | Protected branch rule CRUD | completed | 23d5a3d |
| 4 | mr-04 | Git push enforcement | completed | (see final commit) |
| 5 | mr-05 | Storage diff/mergeability/merge | completed | (see final commit) |
| 6 | mr-06 | Merge request core API + UI shell | completed | (see final commit) |
| 7 | mr-07 | Approvals and merge gates | completed | (see final commit) |
| 8 | mr-08 | Server-side merge + closes links | completed | (see final commit) |
| 9 | mr-09 | Shared collaboration UI | completed | (see final commit) |
| 10 | mr-10 | Overview comments | completed | (see final commit) |
| 11 | mr-11 | Changes tab + review threads | completed | (see final commit) |
| 12 | mr-12 | Branches settings UI | completed | (see final commit) |
| 13 | mr-13 | Post-push create banner | completed | (see final commit) |
| 14 | mr-14 | MR notifications | completed | (see final commit) |
| 15 | mr-15 | Linked discussions sidebar | completed | (see final commit) |
| 16 | mr-16 | E2E integration script | completed | (see final commit) |

## Verification (final)

- `dotnet test` — all projects green (1050+ tests)
- `pnpm test` — 66/66 passed
- E2E script: `scripts/test-merge-requests-e2e.sh` (requires docker compose stack)

## Notes

- Fixed `DependencyInjectionHelpers` to register `IConfiguration` for startup migration path
- Added `RepositoryDefaultBranchControllerTests` for controller coverage gate
