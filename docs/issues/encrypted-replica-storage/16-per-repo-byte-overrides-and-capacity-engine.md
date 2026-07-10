# Per-repo byte overrides and capacity engine

## Metadata

- ID: ers-16
- Type: AFK
- Status: ready
- Source: docs/prd/encrypted-replica-storage.md

## Parent

[PRD: Encrypted Replica Storage](../../prd/encrypted-replica-storage.md)

## What to build

Allow organizations operating more than three storage nodes to raise per-repository byte limits for repositories fully hosted on org-owned nodes.

**Rules:**

- Override permitted only when all four copies (primary, read, both encrypted) reside on org-owned nodes
- Org must operate more than three healthy contributed nodes
- `MaxBytesOverride` on repository record; enforced at push and provision time
- Capacity engine uses override when scoring placement for eligible repos

Integrate with org quota system so overrides cannot exceed aggregate org contributed capacity.

## Acceptance criteria

- [ ] Repo with all four copies on org nodes and org with >3 nodes can set MaxBytesOverride via API
- [ ] Override rejected when any copy resides on platform or foreign-org node
- [ ] Override rejected when org has ≤3 contributed nodes
- [ ] Push/pre-receive enforces override limit on eligible repos
- [ ] Default platform repo limit unchanged for non-eligible repos
- [ ] Tests cover override grant, rejection cases, and push enforcement
- [ ] Admin/org UI shows override status and eligibility reason

## Blocked by

- [15-cross-org-encrypted-placement-algorithm.md](./15-cross-org-encrypted-placement-algorithm.md)

## User stories covered

- 41 — As an organization owner with more than three storage nodes, I want to configure higher per-repo byte limits for repos fully hosted on my nodes, so that large repositories are supported when I provide sufficient capacity.
- 42 — As the API assigning a new repository, I want storage node selection based on available capacity, repository count, and max bytes per repository, so that placement avoids overcommit.

## Notes

Final Phase 3 slice. Completes capacity-aware placement started in issues 04 and 15.
