# Community-hosted hybrid tracer

## Metadata

- ID: ci-prd-15
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md (gap after ci-15)

## Parent

[PRD: CI/CD Pipelines](../../../docs/prd/ci-cd-pipelines.md)

## What to build

Tracer bullet for **federated compute**: org A enrolls a `CrossOrgAllowed` node; org B's repository runs a job with `runs-on: community-hosted`; the job is claimed by org A's node and completes successfully. Prove hybrid routing distinct from `organization-self-hosted` and `ogb-hosted`.

## Acceptance criteria

- [ ] Integration test: `CrossOrgAllowed` node registered under org A
- [ ] Job on org B repo with `community-hosted` claimed by org A node
- [ ] `OwnOrgOnly` node does not claim `community-hosted` jobs from other orgs
- [ ] Platform `ogb-hosted` node does not claim `community-hosted` jobs
- [ ] Job completes with logs; egress uses platform allowlist only (not org B allowlist)
- [ ] Optional compose scenario documented for manual demo

## Blocked by

- [ci-prd-03-org-compute-integration-test.md](./ci-prd-03-org-compute-integration-test.md)
- [ci-prd-07-firecracker-ogb-hosted-tracer.md](./ci-prd-07-firecracker-ogb-hosted-tracer.md)

## User stories covered

- 10 — Different `runs-on` values within same stage
- 37 — `CrossOrgAllowed` for `community-hosted`

## Notes

- Routing exists in `ClaimPipelineJobQueryHandler`; this slice adds cross-org integration proof on real executor path.
- Can share test harness with ci-prd-03.
