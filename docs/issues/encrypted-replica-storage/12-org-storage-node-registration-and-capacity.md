<!-- forge: #130 -->

# Org storage node registration and capacity

## Metadata

- ID: ers-12
- Type: AFK
- Status: ready
- Source: docs/prd/encrypted-replica-storage.md

## Parent

[PRD: Encrypted Replica Storage](../../prd/encrypted-replica-storage.md)

## What to build

Enable organizations to register contributed storage nodes with capacity and hosting policy controls. Extend platform node admin with the same capacity fields.

**StorageNode extensions:**

```
OwnerOrganizationId: Guid?     // null = platform-operated
MaxBytes: long
UsedBytes: long                 // heartbeat-reported or computed
HostingScope: OwnOrgOnly | CrossOrgAllowed
```

**Org enrollment flow:** org admin creates enrollment token; node registers via existing storage agent path with org ownership binding.

**Platform nodes:** admin configures MaxBytes per platform node.

Enforce capacity at provision and artifact upload time — reject placement when node would exceed MaxBytes.

## Acceptance criteria

- [ ] Migration adds org ownership, MaxBytes, UsedBytes, and HostingScope to storage nodes
- [ ] Org admin API can create enrollment token and list org-owned nodes
- [ ] Registered org node appears in fleet registry with correct OwnerOrganizationId
- [ ] Platform admin can set MaxBytes on platform nodes
- [ ] Provision and artifact upload rejected when node capacity exceeded
- [ ] Heartbeat reports used bytes (or API computes from repo assignments)
- [ ] Tests cover org registration, capacity rejection, and platform admin capacity config

## Blocked by

- [11-phase-1-e2e-and-integration-tests.md](./11-phase-1-e2e-and-integration-tests.md)

## User stories covered

- 32 — As an organization owner, I want to register my own storage nodes with the platform, so that I can self-host authoritative Git storage.
- 33 — As an organization owner, I want to configure each registered node as hosting only my org's repos or opt in to hosting other orgs' repos, so that I control my node's exposure.
- 40 — As a platform administrator, I want to configure maximum capacity per platform storage node, so that fleet capacity is bounded and predictable.

## Notes

Phase 2 entry point. HostingScope enforced in placement issues 13 and 15. Quota credits land in issue 14.
