<!-- forge: #78 -->

# Git facade + HTTPS PAT scenario

## Metadata

- ID: e2e-09
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-regression-framework.md

## Parent

[PRD: Unified E2E Regression Framework](../../prd/e2e-regression-framework.md)

## What to build

Add git integration to the E2E framework and migrate the HTTPS git shell script:

1. **IGitOperations** — push, clone, fetch via system `git` CLI through HAProxy HTTPS path; auto-log commands and stdout/stderr in transcript.
2. **IGitAssertions** — LibGit2Sharp read-only inspection of cloned repos (refs, commits, file content).
3. **Git baseline bucket** — `git/{step-id}.txt` or structured snapshot in baseline bundle.
4. **Scenario parity** with `e2e-https-git-test.sh`:
   - Create repo via API
   - Create write-scoped PAT
   - Push over HTTPS
   - Clone over HTTPS and verify commit content
   - Read-scoped PAT denied on push

Retire `e2e-https-git-test.sh` when baselines committed (final deletion in e2e-20).

## Acceptance criteria

- [ ] Git CLI push/clone succeed through HAProxy git path
- [ ] Git command output appears in operation transcript
- [ ] LibGit2Sharp assertions verify pushed commit without fragile log parsing
- [ ] Read-scoped PAT push fails with expected denial
- [ ] Committed baselines include git state artifacts
- [ ] Scenario covers user stories for HTTPS PAT transport spine

## Blocked by

- [08-identity-seed-auth-journey.md](./08-identity-seed-auth-journey.md)

## User stories covered

- 21, 27, 28, 29, 30, 65

## Notes

- Prior art: `scripts/e2e-https-git-test.sh`, git-https-08 issue.
- Blocks browse (e2e-19) and HA chaos (e2e-15) scenarios needing git-seeded repos.
