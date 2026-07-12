# Org compute enroll → job routing integration test

## Metadata

- ID: ci-prd-03
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md (gap after ci-06, ci-15)

## Parent

[PRD: CI/CD Pipelines](../../../docs/prd/ci-cd-pipelines.md)

## What to build

Prove the **organization-self-hosted** path works as a vertical slice: org owner creates enrollment → agent registers with `OwnOrgOnly` → pipeline job with `runs-on: organization-self-hosted` on that org's repo is claimable only by that node, and not by platform or other-org nodes.

## Acceptance criteria

- [ ] Integration test: org owner creates enrollment token via org API
- [ ] Test agent registers and heartbeats as org node with `OwnOrgOnly`
- [ ] Job on org repo with `organization-self-hosted` is claimed by org node
- [ ] Same job is not claimed by platform node or `OwnOrgOnly` node from different org
- [ ] Job completes with passed/failed status and logs persisted
- [ ] Negative case: non-owner cannot create enrollment token

## Blocked by

- [ci-prd-02-org-compute-settings-ui.md](./ci-prd-02-org-compute-settings-ui.md)

## User stories covered

- 35 — Org owner enrolls compute nodes
- 36 — `OwnOrgOnly` scope for `organization-self-hosted`
- 42 — Org members author pipelines without enrolling nodes

## Notes

- May use in-memory/SQLite API tests with test agent HTTP client, or compose-backed test if lighter to maintain.
- Routing logic exists in `ClaimPipelineJobQueryHandler`; this slice validates the full enroll → claim contract.
