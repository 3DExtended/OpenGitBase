<!-- forge: #110 -->

# F07 merge request regression

## Metadata

- ID: pop-22
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-test-population.md

## Parent

[PRD: E2E Test Population](../../prd/e2e-test-population.md)

## What to build

Expand F07 to **≥50 `@Regression` scenarios**.

Additions beyond smoke:

- mr-16 scenario 7: conflict when target advances → merge disabled
- mr-16 scenario 3: admin allowlisted direct push
- Duplicate MR rejection (open draft/approved same src→tgt)
- Full state machine: cannot merge from Draft; Merged only from Approved
- List/filter by status; branch-ahead-summary
- Review comment API smoke; refresh-shas after push
- Auth matrix on all MR mutating endpoints
- Self-approval denial; writer cannot approve own MR

## Acceptance criteria

- [ ] F07 catalog ≥50 regression rows `done`
- [ ] mr-16 all 10 scenarios covered
- [ ] Git + API baselines for merge and conflict paths
- [ ] Merge request PRD critical stories spot-checked in catalog

## Blocked by

- [10-f07-merge-request-parity-smoke.md](./10-f07-merge-request-parity-smoke.md)
- [04-auth-matrix-theory-runner.md](./04-auth-matrix-theory-runner.md)

## User stories covered

- 76–86 (full depth)

## Notes

- Depends on F03 protected branch rules for some matrix cells.
