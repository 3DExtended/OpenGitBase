# Org storage visual regression

## Metadata

- ID: oss-05
- Type: AFK
- Status: ready
- Source: docs/prd/org-storage-self-service-ui.md

## Parent

[PRD: Organization Storage Self-Service UI](../../../../docs/prd/org-storage-self-service-ui.md)

## What to build

Add **multi-state visual regression fixtures** for the org storage self-service UI using the same extracted components from oss-03 (not duplicated static HTML).

**Gallery states in `__visual__/index.vue`:**

1. Empty — no registered nodes, no active enrollment token panel
2. Enrollment success — bootstrap one-liner / code block visible
3. Registered node — inline edit form open (capacity + hosting scope)
4. Unhealthy node — warning badge styling on a registered node row

**Playwright specs:**

- Snapshot each state (dedicated spec or extend existing org storage visual spec)
- Update or replace prior single-state `org-storage-settings.png` as appropriate

Components receive fixture props only — no live API in visual gallery.

## Acceptance criteria

- [ ] Visual gallery includes four org storage states listed above
- [ ] Playwright snapshots committed for each state
- [ ] `pnpm test:visual` passes for org storage gallery tests
- [ ] Fixtures import oss-03 presentational components (no markup drift from production page)
- [ ] Unhealthy and healthy badge variants visibly distinct in snapshot

## Blocked by

- [oss-03](./oss-03.md) — components and layout must exist before gallery wiring

## User stories covered

- 30 — Visual regression fixtures for org storage UI states

## Notes

- Prior art: org compute visual section, repository byte override dual-state fixtures, existing org storage settings snapshot in shell spec.
- Prefer separate snapshots per state over one monolithic screenshot.
