# Self-host tier placement

## Metadata

- ID: ers-13
- Type: AFK
- Status: ready
- Source: docs/prd/encrypted-replica-storage.md

## Parent

[PRD: Encrypted Replica Storage](../../prd/encrypted-replica-storage.md)

## What to build

Extend Replica Set Planner and repository create/update flows to honor org self-host tiers and placement settings.

**Self-host tiers:**

| Org nodes | Placement |
|-----------|-----------|
| 0 | Platform default (primary+read platform; encrypted prefer cross-org, fallback platform) |
| 1 | Primary+read on org node; 2 encrypted elsewhere |
| 2 | Primary+read on org node A; 1 encrypted on org node B; 1 encrypted elsewhere |
| 3 | All 4 on org nodes (primary+read colocated on one; 1 encrypted each on other two) |

**Org/repo settings:**

- Organization default placement policy (inherited by new repos)
- Per-repo override

Cross-org rule: copies on another org's nodes are always encrypted regardless of tier.

## Acceptance criteria

- [ ] Replica Set Planner assigns placements correctly for tiers 0–3
- [ ] Org with 1 node can create repo with primary+read on owned node and encrypted elsewhere
- [ ] Org with 3 nodes can create fully self-hosted four-copy repo
- [ ] Per-repo override supersedes org default
- [ ] Cross-org node never receives primary or read replica role
- [ ] Planner unit tests cover all tiers, colocation rules, and insufficient org node rejection
- [ ] Integration test covers tier-1 and tier-3 org create flows

## Blocked by

- [12-org-storage-node-registration-and-capacity.md](./12-org-storage-node-registration-and-capacity.md)

## User stories covered

- 34 — As an organization owner with one storage node, I want primary and read replica on my node with encrypted copies elsewhere, so that no plaintext infrastructure I do not own holds my repository data.
- 35 — As an organization owner with two storage nodes, I want primary+read on one node and one encrypted copy on my second node, so that I maximize self-hosting while meeting durability requirements.
- 36 — As an organization owner with three storage nodes, I want all four copies on my nodes, so that my repository is fully self-hosted.
- 37 — As an organization owner, I want org-level placement defaults inherited by new repos with per-repo override, so that fleet configuration is manageable.

## Notes

Tier 0 cross-org encrypted preference fully implemented in issue 15; this issue may use platform fallback until 15 lands.
