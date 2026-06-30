# Merge request E2E scenarios

## Metadata

- ID: e2e-16
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-regression-framework.md

## Parent

[PRD: Unified E2E Regression Framework](../../prd/e2e-regression-framework.md)

## What to build

Migrate merge request shell e2e into C# feature tests with baselines and transcripts. Cover core PRD integration scenarios from `test-merge-requests-e2e.sh`:

1. Protect default branch — writer direct push denied; feature branch push allowed.
2. MR lifecycle — Draft → Open → approvals → Approved → squash merge → Merged.
3. MR with `closes` link resolves linked discussion on merge.
4. Auth matrix spot-checks — public repo anonymous read MR; unauthenticated create 401; private repo anonymous 404 / outsider 403.
5. Push rule rejection with error message substring (when rules enabled in scenario setup).

Use git facade for pushes; API client for MR lifecycle; seeded identities from Tier 1.

Retire `test-merge-requests-e2e.sh` when baselines committed (final deletion in e2e-20).

## Acceptance criteria

- [ ] Protected branch direct push denied for writer
- [ ] Core MR happy path (scenario 2 from MR e2e issue) passes with baselines
- [ ] `closes` discussion link resolves on merge
- [ ] Public/private auth matrix spot-checks pass
- [ ] Human-readable transcript covers full MR workflow
- [ ] Committed baselines for API and git artifacts in scenario

## Blocked by

- [09-git-facade-https-pat-scenario.md](./09-git-facade-https-pat-scenario.md)

## User stories covered

- 31, 65

## Notes

- Prior art: `scripts/test-merge-requests-e2e.sh`, merge-requests issue 16.
- Diff/review UI not fully exercised — API-level review comment smoke optional per MR issue notes.
