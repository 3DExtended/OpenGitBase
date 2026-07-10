# Cross-org encrypted placement algorithm

## Metadata

- ID: ers-15
- Type: AFK
- Status: ready
- Source: docs/prd/encrypted-replica-storage.md

## Parent

[PRD: Encrypted Replica Storage](../../prd/encrypted-replica-storage.md)

## What to build

Capacity-aware placement engine for encrypted replica slots with cross-org preference.

**Selection rules for encrypted replicas:**

1. Prefer nodes with `HostingScope = CrossOrgAllowed` owned by a **different** organization than the repo owner
2. Never place plaintext primary or read replica on another org's node
3. Fall back to platform-operated nodes when no eligible cross-org capacity exists
4. Score candidates by free bytes, current repo count, and configured max bytes per repo

Integrate into Replica Set Planner for tier-0 repos and encrypted slots in tiers 1–2.

## Acceptance criteria

- [ ] Encrypted placement prefers foreign-org nodes when available and opted in
- [ ] Same-org nodes never used for cross-org plaintext roles
- [ ] Platform fallback activates when community capacity exhausted
- [ ] Placement skips nodes that would exceed MaxBytes given repo size estimate
- [ ] Planner unit tests cover cross-org preference, fallback, and same-org exclusion
- [ ] Integration test places encrypted copies on second org's node in two-org compose/dev setup
- [ ] Repo on org A never stores plaintext on org B's nodes

## Blocked by

- [13-self-host-tier-placement.md](./13-self-host-tier-placement.md)
- [14-org-quota-credits-and-placement-settings-ui.md](./14-org-quota-credits-and-placement-settings-ui.md)

## User stories covered

- 38 — As an organization owner hosting nodes for other orgs, I want those copies always encrypted, so that I cannot read other organizations' repository contents.
- 43 — As the API assigning a new repository, I want encrypted replica placement to prefer cross-org nodes when available, so that durability is geographically and organizationally distributed.
- 44 — As the API, I want encrypted replica placement to fall back to platform nodes when community capacity is exhausted, so that repository creation never fails solely due to community shortage.

## Notes

Phase 3 entry. May require dev compose profile with two orgs and four storage nodes for full local testing.
