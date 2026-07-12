# Org storage node capacity API

## Metadata

- ID: oss-01
- Type: AFK
- Status: ready
- Source: docs/prd/org-storage-self-service-ui.md

## Parent

[PRD: Organization Storage Self-Service UI](../../../../docs/prd/org-storage-self-service-ui.md)

## What to build

Add an organization-scoped **update storage node capacity** endpoint so org owners can raise (or lower within limits) the declared `MaxBytes` on org-owned nodes after registration.

Mirror the existing platform admin capacity patch behavior, with additional guards:

- Caller must be an **organization owner**.
- Target node must belong to the organization (`OwnerOrganizationId`).
- Reject `MaxBytes` less than current `UsedBytes` on the node.
- Reject negative values.

Org quota credits continue to derive from the sum of healthy org nodes' `MaxBytes` via the existing quota aggregation query — no separate quota recalculation work required.

Expose the endpoint on the organization storage controller and add a matching method on the web API client module (for oss-03).

## Acceptance criteria

- [ ] Org owner can PATCH capacity on an org-owned storage node and receives updated node DTO
- [ ] Non-owner org member receives 403
- [ ] PATCH for a node not owned by the organization returns 404
- [ ] PATCH with `MaxBytes` below current `UsedBytes` returns 400 with clear error
- [ ] PATCH with negative `MaxBytes` returns 400
- [ ] Increasing `MaxBytes` on a healthy node increases org contributed capacity in settings/quota readback
- [ ] Controller or integration tests cover owner success, 403, 404, and used-bytes floor
- [ ] Handler unit test covers used-bytes validation if logic lives in handler layer

## Blocked by

- None — can start immediately

## User stories covered

- 15 — Max capacity edits rejected when below current used bytes
- 16 — Increase max capacity freely when adding disk
- 17 — Decrease requiring migration deferred (v1 rejects below used; ers-17 follow-up)
- 32 — Org storage management restricted to owners

## Notes

- Reuse existing platform `UpdateStorageNodeCapacity` handler where possible; extend validation for used-bytes floor (platform admin path may remain unrestricted for v1).
- No schema migration expected.
- Compare authorization pattern with existing org hosting-scope PATCH on the same controller.
