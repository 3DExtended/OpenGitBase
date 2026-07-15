<!-- forge: #98 -->

# F07 merge request parity smoke

## Metadata

- ID: pop-10
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-test-population.md

## Parent

[PRD: E2E Test Population](../../prd/e2e-test-population.md) · Parity: [mr-16](../merge-requests/16-e2e-merge-request-integration-tests.md)

## What to build

Close **mr-16 core happy-path smoke** — **10 `@Smoke` scenarios** for merge requests.

Minimum scenarios:

1. Protect `main`: writer push denied; feature branch push allowed (extend existing)
2. Unprotected repo: direct push works
3. Draft → publish Open
4. Approvals → Approved → squash merge → Merged (end-to-end)
5. MR with `closes` link resolves discussion on merge
6. Public anon read MR; anon create 401
7. Private anon 404; outsider 403
8. Push rule rejection with message substring
9. Create MR when source ahead of target
10. Force-push dismisses approvals

Git operations via HTTPS PAT; API baselines for MR state transitions.

## Acceptance criteria

- [ ] mr-16 scenarios 2, 5, 6 pass reliably (PRD priority)
- [ ] ≥10 smoke scenarios tagged and cataloged
- [ ] Git transcript captured on push/merge paths
- [ ] Runner smoke filter passes for MergeRequest category

## Blocked by

- [02-shared-fixture-library.md](./02-shared-fixture-library.md)
- [03-git-testdata-provisioning.md](./03-git-testdata-provisioning.md)

## User stories covered

- 76–84 (MR smoke subset)

## Notes

- Conflict scenario (mr-16 #7) deferred to pop-22 regression.
