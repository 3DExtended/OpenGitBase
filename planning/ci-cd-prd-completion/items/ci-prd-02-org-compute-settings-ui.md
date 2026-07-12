# Org compute settings UI + enrollment API hardening

## Metadata

- ID: ci-prd-02
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md (gap after ci-06)

## Parent

[PRD: CI/CD Pipelines](../../../docs/prd/ci-cd-pipelines.md)

## What to build

Complete **org owner self-service compute enrollment** as described in user docs (`/docs/ci/compute-nodes`): an organization settings page where owners create enrollment tokens, choose **Hosting Scope**, declare capacity, and manage registered org nodes.

Harden the org enrollment API to match storage-node patterns: owner-only authorization, server-set `CreatedByUserId`, org-scoped list endpoint, and capacity update for org-owned nodes.

## Acceptance criteria

- [ ] Org settings route (e.g. `/{owner}/compute`) visible only to org owners
- [ ] UI creates enrollment tokens with `HostingScope` (`OwnOrgOnly` or `CrossOrgAllowed`) and required capacity fields
- [ ] UI lists org-registered compute nodes with health and utilization
- [ ] Org owners can update node capacity; reductions rejected while running jobs exceed new limits
- [ ] Non-owner org members cannot create tokens or change capacity (API + UI)
- [ ] `CreatedByUserId` set from authenticated session, not request body
- [ ] Web API client methods added for org compute enrollment and node list
- [ ] User docs link from org compute page matches implemented behavior

## Blocked by

- None — can start immediately

## User stories covered

- 35 — Org owner enrolls **Compute Nodes** without platform admin approval
- 36 — Set `HostingScope` to `OwnOrgOnly`
- 37 — Set `HostingScope` to `CrossOrgAllowed`
- 38 — Require capacity at enrollment
- 39 — Update capacity later
- 40 — Capacity reductions rejected while jobs exceed new limits
- 42 — Org members use `organization-self-hosted` without enrolling nodes

## Notes

- Compare with `OrganizationStorageController` owner checks and `/{owner}/storage` page structure.
- ci-06 shipped API-only; acceptance criteria in `docs/issues/ci-cd-pipelines/06-*.md` remain unchecked.
