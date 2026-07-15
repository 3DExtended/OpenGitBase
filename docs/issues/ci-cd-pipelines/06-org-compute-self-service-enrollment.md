<!-- forge: #39 -->

# Org Owner self-service compute enrollment

## Metadata

- ID: ci-06
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md

## Parent

[PRD: CI/CD Pipelines (Firecracker + Hybrid Compute)](../../prd/ci-cd-pipelines.md)

## What to build

Extend the **Compute Node Registry** so organization **Owners** enroll compute nodes without platform admin approval. Owners choose **Hosting Scope** (`OwnOrgOnly` for `organization-self-hosted`, `CrossOrgAllowed` for `community-hosted`), set required capacity at enrollment, and manage tokens from org settings. Org members can author pipelines using `organization-self-hosted` without enrolling nodes themselves.

## Acceptance criteria

- [ ] Org Owner API/UI creates enrollment tokens scoped to their organization
- [ ] Registration sets `HostingScope` to `OwnOrgOnly` or `CrossOrgAllowed`
- [ ] `OwnOrgOnly` nodes appear only for jobs with `runs-on: organization-self-hosted` on that org's repos
- [ ] `CrossOrgAllowed` nodes are eligible for `runs-on: community-hosted` across orgs
- [ ] Required capacity fields enforced at org enrollment same as platform enrollment
- [ ] Non-owner org members cannot create enrollment tokens
- [ ] Integration test: org owner enrolls node → eligible for correct hosting profile only

## Blocked by

- [05-compute-node-registry-platform-enrollment.md](./05-compute-node-registry-platform-enrollment.md) (ci-05)

## User stories covered

- 35 — Org owner enrolls **Compute Nodes** without platform admin approval.
- 36 — Set `HostingScope` to `OwnOrgOnly`.
- 37 — Set `HostingScope` to `CrossOrgAllowed`.
- 42 — Org members use `organization-self-hosted` without enrolling nodes.

## Notes

- Org agents use HTTPS long-poll claim only (story 41) — implemented in ci-08.
- Domain allowance approval for org egress is ci-16.
