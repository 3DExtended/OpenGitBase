<!-- forge: #88 -->

# Runner documentation + shell script retirement

## Metadata

- ID: e2e-20
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-regression-framework.md

## Parent

[PRD: Unified E2E Regression Framework](../../prd/e2e-regression-framework.md)

## What to build

Finalize the framework as the single regression entry point:

1. **Documentation** — README section (or dedicated doc) covering:
   - `dotnet run` entry point on runner project
   - Compose profiles (fast vs full-ha)
   - Tier model and failure semantics
   - All CLI flags (`--update-baselines`, `--open-report`, `--no-open-report`, `--fuzz`, `--filter`)
   - Baseline workflow for new and discovered tests
   - Local report location (not committed)
2. **Delete migrated shell e2e scripts** once C# parity baselines exist:
   - `e2e-https-git-test.sh`
   - `test-merge-requests-e2e.sh`
   - `test-ha-storage-e2e.sh` / `test-ha-storage-compose.sh` (as applicable)
   - `test-discussions-e2e.sh`
   - `test-repo-browse-e2e.sh`
3. Update any references in docs/issues/READMEs that pointed to deleted scripts to reference runner instead.
4. Confirm no remaining top-level `scripts/*e2e*` entry points for migrated scenarios.

## Acceptance criteria

- [ ] Developer doc explains full local regression workflow end-to-end
- [ ] All listed shell e2e scripts removed from repository
- [ ] Docs/issues referencing old scripts updated to runner command
- [ ] Single discoverable entry point — no orphaned shell regression scripts for migrated domains
- [ ] `dotnet run` on runner executes full default suite (fast profile, no fuzz)

## Blocked by

- [09-git-facade-https-pat-scenario.md](./09-git-facade-https-pat-scenario.md)
- [15-ha-storage-chaos-scenarios.md](./15-ha-storage-chaos-scenarios.md)
- [16-merge-request-e2e-scenarios.md](./16-merge-request-e2e-scenarios.md)
- [18-discussion-e2e-scenarios.md](./18-discussion-e2e-scenarios.md)
- [19-repository-browse-e2e-scenarios.md](./19-repository-browse-e2e-scenarios.md)

## User stories covered

- 1, 64

## Notes

- Do not remove storage-layer Python/shell integration tests in `applications/repo-storage-layer/` — out of unified runner v1 scope per PRD.
- CI integration intentionally deferred; doc should mention `--no-open-report` for future pipeline use.
