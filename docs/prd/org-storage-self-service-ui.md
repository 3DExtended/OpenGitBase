# PRD: Organization Storage Self-Service UI

**Status:** Design complete (grill-me session, July 2026).  
**Parent:** [Encrypted Replica Storage](./encrypted-replica-storage.md) — Phase 2 issue **ers-14**.  
**Related backlog:** [ers-17](../issues/encrypted-replica-storage/17-org-node-capacity-shrink-via-rebalance.md) (capacity shrink via rebalance).  
**Domain glossary:** [CONTEXT.md](../../CONTEXT.md).

---

## Problem Statement

OpenGitBase supports organization-contributed **storage nodes**: org owners can earn **quota credits**, influence **placement policy**, and self-host authoritative Git storage under the four-copy encrypted replication model. The control-plane APIs for org storage enrollment, node listing, hosting scope, and placement settings already exist.

However, org owners cannot complete the registration loop through the web UI today:

- The organization storage page shows quota credits, placement defaults, and a read-only node list — but **no enrollment creation**, **no install guidance**, and **no post-registration node management**.
- Registration currently requires calling `POST /organization/{id}/storage/enrollments` manually, then configuring a storage agent with PKI material and fleet secrets without a guided path.
- Org compute already provides the intended self-service pattern (enrollment form, one-time token, operator docs). Storage lacks parity, blocking user story 32 and leaving ers-14 acceptance criteria unmet.

Operators on a generic Linux machine need a **copy-paste bootstrap path** that clones the project, builds the storage agent image, generates node certificates, fetches the platform dispatcher SSH public key, and starts a healthy node — without platform admin involvement.

---

## Solution

Deliver the full **ers-14** surface: org storage self-service UI, org-scoped capacity API, operator documentation, bootstrap script, and visual regression coverage — shipped as five sequential work items on the default branch.

**Local work items:** [planning/prd-issues-tdd-local-main/org-storage-self-service-ui/](../../planning/prd-issues-tdd-local-main/org-storage-self-service-ui/index.md) (`oss-01` … `oss-05`).

### Operator journey

1. Org **owner** opens **Organization settings → Storage** (`/{org}/storage`).
2. Creates an **enrollment** with Node ID, max capacity (GiB), and hosting scope.
3. Copies a **prefilled bootstrap invocation** or downloads a generated shell script.
4. Runs the script on a Linux host with Docker; the storage agent registers and begins heartbeats.
5. The node appears under **Registered nodes** with health, used/max bytes, and hosting scope.
6. Owner can **edit** max capacity (bounded by current usage) and hosting scope inline.
7. **Quota credits** and **placement defaults** remain on the same page for policy context.

### Delivery phases (implementation order)

| Item | Scope |
|------|--------|
| **oss-01** | Org capacity PATCH API with used-bytes guard |
| **oss-02** | Canonical bootstrap script in repository |
| **oss-03** | Org storage page UI (components, enrollment, node edit, 403 handling, i18n) |
| **oss-04** | `/docs/storage/*` section |
| **oss-05** | Multi-state visual gallery; snapshot updates |

See [work item index](../../planning/prd-issues-tdd-local-main/org-storage-self-service-ui/index.md) for dependencies.

---

## User Stories

### Access and navigation

1. As an organization **owner**, I want a Storage link under Organization settings in the sidebar, so that I can manage contributed storage without knowing the URL.
2. As an organization **member** (non-owner), I want a clear message when I open the storage settings URL directly, so that I understand only owners can manage storage fleet resources.
3. As an organization **owner**, I want the storage settings page header to link to operator documentation, so that I can read full install and networking guidance.

### Quota and placement context

4. As an organization owner, I want to see platform byte limit, contributed capacity, and effective org byte limit on the storage page, so that I understand the benefit of contributing nodes.
5. As an organization owner, I want to configure default placement policy and self-host preference on the same page, so that new repositories inherit sensible fleet defaults.
6. As an organization owner, I want contributed capacity to update when I register healthy nodes with declared max bytes, so that quota credits reflect my fleet contribution.

### Enrollment

7. As an organization owner, I want to create a storage node enrollment from the UI with Node ID, max capacity, and hosting scope, so that I do not need API tools or platform admin approval.
8. As an organization owner, I want enrollment expiry to default to a reasonable window (seven days) without exposing an expiry field, so that the form stays simple.
9. As an organization owner, I want to see a one-time enrollment token with a copy-paste bootstrap command, so that I can install the agent immediately.
10. As an organization owner, I want to download a shell script with my enrollment values baked in, so that I can run setup offline or archive it securely.
11. As an organization owner, I want to list pending and consumed enrollments, so that I can audit which tokens were used.
12. As an organization owner, I want hosting scope choices explained as own-org-only versus cross-org encrypted replica hosting, so that I understand exposure before enrolling.

### Registered node management

13. As an organization owner, I want registered org-owned nodes to show health status, used bytes, max bytes, and hosting scope, so that I can monitor my fleet.
14. As an organization owner, I want to edit a registered node's max capacity and hosting scope from the UI, so that I can adjust contribution without re-enrolling.
15. As an organization owner, I want max capacity edits rejected when below current used bytes, so that I cannot overcommit a node that already stores data.
16. As an organization owner, I want to increase max capacity freely when I add disk, so that quota credits grow with my hardware.
17. As a platform operator, I want org capacity decreases that require data migration deferred to a future rebalance workflow, so that v1 stays safe and predictable.

### Bootstrap and agent install

18. As an organization owner, I want a bootstrap script that runs on a generic Linux machine with Docker, git, openssl, and curl, so that I do not need the platform compose stack locally.
19. As an organization owner, I want the script to shallow-clone the OpenGitBase repository and build the storage agent image, so that I use the same agent as the platform fleet without relying on a published container registry (v1).
20. As an organization owner, I want the script to generate node PKI material for my chosen Node ID, so that registration satisfies certificate thumbprint requirements.
21. As an organization owner, I want the script to fetch the fleet dispatcher SSH public key using my enrollment token, so that git dispatchers can authenticate to my node without admin credentials.
22. As an organization owner, I want to specify the hostname or IP address platform dispatchers should use (`internalHost`), so that my node is reachable for git and replication traffic.
23. As an organization owner, I want default ports published (SSH, internal HTTP, git HTTP, peer mTLS) with optional overrides, so that standard setups work out of the box and advanced NAT setups remain possible.
24. As an organization owner, I want documentation describing firewall rules and non-default port/NAT configurations, so that registration failures due to networking are diagnosable.

### Documentation

25. As an organization owner, I want a dedicated storage documentation section separate from CI docs, so that storage self-hosting is not confused with compute runners.
26. As an organization owner, I want a guide covering enrollment, bootstrap script usage, hosting scope, quota credits, and networking, so that I can onboard a node without support.
27. As a CI pipeline author, I want CI documentation to cross-link storage documentation clarifying separate agent fleets, so that I do not conflate compute and storage enrollment.

### Platform and admin parity

28. As a platform administrator, I want org-contributed nodes to appear in the fleet registry with correct owner organization binding, so that placement and quota logic treat them distinctly from platform nodes.
29. As a platform administrator, I want org enrollment to reuse the existing storage agent registration path, so that fleet behavior stays uniform.
30. As a developer, I want visual regression fixtures for org storage UI states, so that layout changes to enrollment and node management are caught in CI.

### Security and trust

31. As a security reviewer, I want enrollment tokens shown once and never retrievable afterward, so that token leakage risk is minimized.
32. As a security reviewer, I want org storage management endpoints restricted to organization owners, so that members cannot mint enrollments or change hosting scope.
33. As a security reviewer, I want the bootstrap dispatcher-key endpoint to require a valid enrollment token and node ID, so that anonymous callers cannot harvest fleet SSH keys.

---

## Implementation Decisions

### Work packaging

Ship as five stacked items on the default branch: **oss-01** (API), **oss-02** (script), **oss-03** (UI), **oss-04** (docs), **oss-05** (visuals). See [work item index](../../planning/prd-issues-tdd-local-main/org-storage-self-service-ui/index.md).

### Backend — org node capacity update (ers-14a)

- Add an organization-scoped **update node capacity** command and HTTP endpoint mirroring the existing platform admin capacity patch.
- **Authorization:** organization owner only; node must belong to the organization (`OwnerOrganizationId`).
- **Validation:** reject `MaxBytes` less than current `UsedBytes` on the node; reject negative values.
- **Side effect:** org quota credits recalculate automatically via existing contributed-capacity aggregation over healthy org nodes.
- Reuse the existing platform capacity update handler logic where possible; extend with org-ownership verification at the controller or a dedicated org-scoped handler wrapper.

**Assumption:** no new schema migration required; `MaxBytes` and `UsedBytes` already exist on storage nodes.

### Backend — existing endpoints (no change expected)

- Create/list enrollments, list org nodes, update hosting scope, read/update org storage settings — already implemented on the organization storage controller.
- Bootstrap dispatcher SSH public key — already exposed anonymously with enrollment token + node ID headers.

### Bootstrap script (ers-14a)

- Canonical script lives in the repository scripts directory (alongside fleet bootstrap tooling).
- **Inputs:** enrollment token, node ID, API base URL, internal host; optional port overrides for SSH, internal HTTP, git HTTP, and peer mTLS.
- **Flow:**
  1. Verify prerequisites (docker, git, openssl, curl).
  2. Shallow clone OpenGitBase source.
  3. Generate per-node PKI (CA + node certificate/key) for the enrolled node ID.
  4. Fetch dispatcher SSH public key from the storage-node bootstrap API.
  5. Build the storage agent container image from the repo-storage-layer Dockerfile.
  6. Run the container with published ports, cert mounts, repos volume, and enrollment environment.
- **Delivery from UI:** prefilled `curl | bash -s -- …` one-liner plus downloadable script with substituted values (same canonical script, two consumption modes).

**Assumption (v1):** no pre-published container image; shallow clone + local build is the supported install path until a registry image exists.

### Web UI — page structure (ers-14b)

- Extend the organization storage page at `/{owner}/storage`.
- **Section order:** Quota credits → Registered nodes → Enrollments → Placement defaults.
- **Page header:** title, short description, link to `/docs/storage/org-storage-nodes`.
- **Non-owner access:** detect HTTP 403 from org storage APIs; show owner-only message; hide management UI (parity with organization compute page).

### Web UI — extracted components (ers-14b)

Presentational components with props and events; page owns API calls and state:

- Quota credits summary card
- Registered node list with inline edit affordance
- Enrollment creation form and enrollment history list
- Enrollment success panel (one-liner + download + docs link)
- Placement defaults form (existing behavior, relocated to bottom)

Registered node **inline edit:** Edit expands max capacity (GiB input) and hosting scope selector; Save/Cancel. Separate API calls for capacity and hosting scope are acceptable behind a single Save action.

### Web UI — enrollment form fields (ers-14b)

- **Required:** Node ID, max capacity (GiB, converted to bytes for API).
- **Optional with default:** hosting scope (`OwnOrgOnly` default).
- **Hidden:** expires in hours (server default 168).

Hosting scope labels must use storage-specific copy (own org repos vs cross-org encrypted replicas), not CI compute profile wording.

### Documentation (ers-14c)

- New **`/docs/storage/*`** section with its own docs page registry (parallel to CI docs, not nested under `/docs/ci/`).
- Initial page: **Organization storage nodes** — enrollment, bootstrap script, networking/firewall, hosting scope, quota credits, relationship to self-host tiers.
- Docs index lists the new section; CI overview cross-links storage docs for fleet separation.

### Visual regression (ers-14c)

Multi-state fixtures in the visual gallery using the same extracted components:

- Empty state (no registered nodes)
- Enrollment success with bootstrap one-liner visible
- Registered node with inline edit form open
- Unhealthy node badge styling

Update existing org storage settings snapshot or split into focused snapshots per state.

### Deep modules (testable interfaces)

| Module | Responsibility | Interface shape |
|--------|----------------|-----------------|
| Org storage capacity command | Validate and persist org-owned node max bytes | Query/command: node ID, org ID, new max bytes → node DTO or validation error |
| Org storage quota aggregation | Sum healthy org node max bytes for quota credits | Existing query; consumed by settings enrichment |
| Bootstrap script | Idempotent node install on Linux | CLI args → running container + registered node |
| Org storage UI components | Render fleet state without API knowledge | Props: nodes, enrollments, settings, errors; events: create enrollment, save node, save placement |
| Storage docs registry | Serve operator markdown pages | Slug → page content (mirrors CI docs registry pattern) |

---

## Testing Decisions

**Principle:** Test externally observable behavior — HTTP status codes, response bodies, UI states, script exit codes — not internal wiring.

### Backend (ers-14a)

- Controller or integration tests: org owner can patch capacity; non-owner receives 403; max below used bytes receives 400; node not in org receives 404.
- Handler unit tests for capacity floor validation (prior art: existing storage node and compute node capacity handler tests).
- Optional script smoke test in CI: dry-run or mocked API flags validating argument parsing and prerequisite checks (full docker build may remain manual/integration-only if too heavy for default CI).

### Web (ers-14b)

- Component or page tests for 403 forbidden state, enrollment form submission payload shape (GiB → bytes), and inline edit save handlers (prior art: organization compute page and repository byte override panel tests).
- API client tests if new org capacity method added to the web API module.

### Visual (ers-14c)

- Playwright snapshots for each gallery state (prior art: org compute visual spec, org storage settings shell spec, repository byte override dual-state fixtures).
- Run `pnpm test:visual` when UI components or gallery fixtures change.

### Out of manual scope for this PRD

- End-to-end test requiring a live org node on the public internet (document in test plan; optional compose-based integration later).

---

## Out of Scope

- **Capacity shrink below used bytes via live migration** — deferred to ers-17 (drain node + platform rebalance before decrease).
- **Pre-published storage container image** — v1 uses shallow clone + build; registry pull may be added later as an optimization.
- **Platform admin UI changes** for org nodes (admin storage page remains platform-fleet focused unless minor parity is trivial).
- **Per-repo placement override UI** — already delivered elsewhere; not part of this PRD except as inherited placement defaults on the org storage page.
- **Enrollment approval workflow** — org owners self-serve without platform admin gate (matches org compute).
- **Bare-metal install without Docker** — script targets Docker; bare-metal documented as future/advanced only.
- **Automatic DNS or tunnel provisioning** — owner supplies reachable `internalHost`; NAT/reverse-proxy is documented manual configuration.

---

## Further Notes

### Design session decisions (reference)

| Topic | Decision |
|-------|----------|
| Scope | Full ers-14 (enrollment UI, node ops, docs, script, visuals) |
| MaxBytes edit | New max ≥ current used bytes |
| Docs location | New `/docs/storage/*` section |
| Bootstrap | Unified shell script; shallow clone + docker build |
| Script delivery | Repo script + prefilled one-liner + download |
| Networking | Default host + standard ports; optional overrides; firewall in docs |
| Page order | Quota → Nodes → Enrollments → Placement |
| Components | Extracted presentational modules shared with visual gallery |

### Dependency on encrypted replica storage Phase 2

This PRD completes the **operator-facing** half of Phase 2 issue ers-14. Backend registration, quota credits, and placement settings API largely exist from ers-12/14 backend work; this effort closes the UI, capacity PATCH gap, bootstrap ergonomics, and documentation.

### Risks

- **Networking misconfiguration** is the most likely user failure mode; docs and script output must prominently require a dispatcher-reachable `internalHost`.
- **Compose override confusion** does not apply to org external nodes; script must not reference platform compose override patterns.
- **Token single-display** — UI must warn that bootstrap tokens cannot be retrieved after navigation (same pattern as admin storage and org compute).

### Success criteria (release checklist)

- [ ] Org owner can enroll, bootstrap, and see a healthy node on `/{org}/storage` without admin involvement
- [ ] Org owner can edit max capacity (increase; decrease blocked at used floor) and hosting scope
- [ ] Non-owner receives clear 403 UX
- [ ] Bootstrap script succeeds on a clean Linux VM with Docker against a running instance
- [ ] Storage docs section published at `/docs/storage/org-storage-nodes`
- [ ] Visual gallery covers empty, enrollment success, edit open, and unhealthy states
- [ ] Backend tests cover org capacity PATCH authorization and validation
