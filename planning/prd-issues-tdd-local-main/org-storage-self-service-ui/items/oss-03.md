# Org storage self-service UI

## Metadata

- ID: oss-03
- Type: AFK
- Status: ready
- Source: docs/prd/org-storage-self-service-ui.md

## Parent

[PRD: Organization Storage Self-Service UI](../../../../docs/prd/org-storage-self-service-ui.md)

## What to build

Complete the organization storage settings page so owners can enroll nodes, install agents, and manage registered fleet resources — parity with organization compute self-service.

**Page:** `/{owner}/storage` (sidebar link for org owners already exists).

**Section order:**

1. Quota credits
2. Registered nodes
3. Enrollments
4. Placement defaults

**Header:** title, description, link to `/docs/storage/org-storage-nodes` (route lands in oss-04; stub or placeholder link acceptable until oss-04 merges).

**Access:** detect 403 from org storage APIs; show owner-only message; hide management UI (organization compute parity).

**Extracted presentational components** (props + events; page owns fetch/mutations):

- Quota credits card
- Registered node list with inline edit
- Enrollment form, history list, and success panel
- Placement defaults form (relocate existing behavior to bottom)

**Enrollment form:**

- Node ID, max capacity (GiB → bytes for API), hosting scope (default `OwnOrgOnly`)
- Hide expiry field (server default 168 hours)
- Storage-specific hosting scope labels (own org vs cross-org encrypted replicas)

**Enrollment success panel:**

- One-time token warning
- Prefilled bootstrap one-liner referencing canonical script from oss-02
- Download generated `.sh` with values substituted
- Copyable code block for full setup

**Registered nodes:**

- Health badge, used/max bytes, hosting scope badge
- Edit → max GiB + hosting scope → Save/Cancel (capacity via oss-01 API, scope via existing PATCH)

**i18n:** extend `org.storage.*` strings (mirror depth of `org.compute.*`).

## Acceptance criteria

- [ ] Org owner can create enrollment from UI; token shown once
- [ ] Enrollment success shows bootstrap one-liner and download action
- [ ] Enrollments list shows active vs consumed tokens
- [ ] Registered nodes show health, used/max, hosting scope
- [ ] Inline edit saves capacity and hosting scope; capacity below used bytes shows API error
- [ ] Non-owner sees forbidden message; no enrollment or edit controls
- [ ] Page section order matches spec (quota → nodes → enrollments → placement)
- [ ] Web API client includes org storage capacity update method
- [ ] Component or page tests cover 403 state, GiB→bytes enrollment payload, and save handlers

## Blocked by

- [oss-01](./oss-01.md) — capacity PATCH for node edit
- [oss-02](./oss-02.md) — real bootstrap script URL and argument shape for success panel

## User stories covered

- 2 — Non-owner clear message on direct URL access
- 3 — Header link to operator documentation
- 4–6 — Quota credits and placement defaults on page
- 7–12 — Enrollment creation, token display, enrollment list, hosting scope copy
- 13–14 — Registered node display and inline edit
- 31 — Token shown once
- 32 — Owner-only management

## Notes

- Compare implementation with organization compute page and admin storage enrollment panel.
- Sidebar Storage nav item already gated on `isOrgOwner` — no change expected.
- User story 1 (sidebar link) pre-satisfied.
