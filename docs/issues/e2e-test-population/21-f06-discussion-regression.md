# F06 discussion regression

## Metadata

- ID: pop-21
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-test-population.md

## Parent

[PRD: E2E Test Population](../../prd/e2e-test-population.md)

## What to build

Expand F06 to **≥50 `@Regression` scenarios**.

Additions beyond smoke:

- Sub-thread create, nested reply, resolve (discussion-sub-threads PRD)
- Dismiss vs resolve permission matrix
- Edit/delete comment permissions
- Mention-triggered notifications; subscription opt-out
- Full auth matrix on discussion endpoints (6 actors × operations)
- Anchor outdated path smoke
- Tag CRUD and filter combinations

## Acceptance criteria

- [ ] F06 catalog ≥50 regression rows `done`
- [ ] Sub-thread scenarios ≥5 with baselines
- [ ] Notification side-channel baselines ≥3
- [ ] disc-10 fully satisfied including items deferred from smoke

## Blocked by

- [11-f06-discussion-parity-smoke.md](./11-f06-discussion-parity-smoke.md)
- [04-auth-matrix-theory-runner.md](./04-auth-matrix-theory-runner.md)

## User stories covered

- 73–75, 65–72 (full depth)

## Notes

- Largest discussion slice; coordinate with F07 `closes` link tests.
