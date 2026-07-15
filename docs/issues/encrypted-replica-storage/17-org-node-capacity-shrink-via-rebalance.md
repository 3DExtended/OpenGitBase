<!-- forge: #135 -->

# Org node capacity shrink via platform rebalance

## Metadata

- ID: ers-17
- Type: discussion
- Status: backlog
- Source: org storage UI design session (ers-14 follow-up)

## Parent

[PRD: Encrypted Replica Storage](../../prd/encrypted-replica-storage.md)

## Problem

Org-owned storage node **MaxBytes** edits will enforce **Option A**: new max must be ≥ current `usedBytes`. Owners cannot shrink a node below what it already stores.

That is safe for v1, but real operators will eventually want to **reduce declared capacity** (decommission disk, right-size hardware, revoke contributed quota). That requires **moving repositories off the node first** — within the platform — before the capacity floor allows a decrease.

## Open questions (needs design discussion)

1. **Trigger** — Owner-initiated shrink request, automated rebalance job, or admin-assisted migration?
2. **Scope** — Move only org-owned repos, or also encrypted replica copies hosted on a community node?
3. **Placement** — Target selection: other org nodes first, then platform fleet, respecting self-host tier and RF=4 invariants?
4. **Availability** — Read-only window vs live migration with replication backfill?
5. **Quota** — When does contributed quota credit decrease: on successful migration completion or immediately on shrink approval?
6. **UI** — Warning on capacity edit (“N repos must be moved first”), dedicated “Drain node” action, or rebalance queue in admin UI?
7. **Failure** — Partial migration rollback, stuck rebalance, node marked unhealthy mid-drain.

## Proposed direction (draft — not decided)

- Add a **“drain node”** org-owner action that marks the node no-new-placements, enqueues rebalance for assigned repos, then allows MaxBytes decrease once `usedBytes` fits.
- Reuse existing delete/rebalance/anti-entropy machinery from issue 09 where possible.

## Acceptance criteria (TBD after discussion)

- [ ] Design doc or ADR captures shrink/drain workflow and quota timing
- [ ] Org owner can shrink MaxBytes after repos are moved off the node
- [ ] Platform rejects shrink when repos still assigned and used bytes exceed new max (already true in ers-14 UI/API)
- [ ] Tests cover happy path and blocked shrink with assigned repos

## Blocked by

- [14-org-quota-credits-and-placement-settings-ui.md](./14-org-quota-credits-and-placement-settings-ui.md) (org storage UI + capacity edit)
- [09-delete-rebalance-and-anti-entropy-extensions.md](./09-delete-rebalance-and-anti-entropy-extensions.md)

## Related

- ers-14 org storage UI: MaxBytes floor at `usedBytes` (decided)
- Issue 09 rebalance engine

## Notes

Captured during grill-me session for org self-service storage enrollment UI. Do not implement until product/ops workflow is agreed.
