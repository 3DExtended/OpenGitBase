# Org quota credits and placement settings UI

## Metadata

- ID: ers-14
- Type: AFK
- Status: ready
- Source: docs/prd/encrypted-replica-storage.md

## Parent

[PRD: Encrypted Replica Storage](../../prd/encrypted-replica-storage.md)

Detailed self-service UI spec: [PRD: Organization Storage Self-Service UI](../../prd/org-storage-self-service-ui.md)

## What to build

Link contributed storage capacity to organization byte limits and expose placement settings in the web UI.

**Quota credits:** org byte limit increases proportionally to sum of MaxBytes on org-contributed healthy nodes (formula documented in implementation; e.g. contributed capacity adds to org quota ceiling).

**UI surfaces:**

- Org settings: default placement policy and self-host preference
- Storage node detail (org and admin): MaxBytes, HostingScope, used bytes, owned repos count
- Repo settings: placement policy override (inherits org default)

API endpoints for reading and updating org storage settings and per-node HostingScope.

## Acceptance criteria

- [ ] Org byte limit reflects contributed node capacity per documented formula
- [ ] Org admin can view and edit default placement policy
- [ ] Org admin can set HostingScope per org-owned node
- [ ] Repo settings show inherited placement policy with override control
- [ ] Admin can view platform node capacity configuration
- [ ] Tests cover quota calculation, settings inheritance, and override persistence
- [ ] UI smoke test for org storage settings page

## Blocked by

- [12-org-storage-node-registration-and-capacity.md](./12-org-storage-node-registration-and-capacity.md)

## User stories covered

- 37 — As an organization owner, I want org-level placement defaults inherited by new repos with per-repo override, so that fleet configuration is manageable.
- 39 — As an organization owner, I want contributed storage capacity to increase my organization's byte limit, so that hosting nodes provides tangible benefit.

## Notes

Exact quota formula is an implementation detail; document in code and PRD assumptions. Can ship API before full UI if needed.
