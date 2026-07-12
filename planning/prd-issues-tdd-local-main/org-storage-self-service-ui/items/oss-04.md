# Storage operator documentation

## Metadata

- ID: oss-04
- Type: AFK
- Status: ready
- Source: docs/prd/org-storage-self-service-ui.md

## Parent

[PRD: Organization Storage Self-Service UI](../../../../docs/prd/org-storage-self-service-ui.md)

## What to build

Add a **storage documentation section** parallel to CI docs (`/docs/storage/*`), not nested under `/docs/ci/`.

**Initial content — Organization storage nodes:**

- Self-service enrollment from org settings
- Bootstrap script usage (canonical script from oss-02): required args, optional port overrides, prerequisites
- Networking and firewall: dispatcher-reachable `internalHost`, default ports, NAT/reverse-proxy notes
- Hosting scope semantics (own org vs cross-org encrypted replicas)
- Quota credits and relationship to self-host tiers
- Fleet separation: storage agents vs compute agents

**Routing and registry:**

- New docs page registry mirroring CI docs pattern (slug → markdown content)
- `/docs/storage` index redirect or listing
- Main docs index includes storage section

**Cross-links:**

- Org storage page header links to `/docs/storage/org-storage-nodes`
- CI overview / compute-nodes docs link back to storage docs for fleet separation

## Acceptance criteria

- [ ] `/docs/storage/org-storage-nodes` renders markdown guide
- [ ] Docs index lists storage section
- [ ] Org storage page docs link resolves correctly
- [ ] CI docs mention storage as separate fleet with link to storage docs
- [ ] Bootstrap script flags and networking requirements match oss-02 implementation
- [ ] Registry test or slug listing test if CI docs has prior art

## Blocked by

- [oss-02](./oss-02.md) — document actual script interface
- [oss-03](./oss-03.md) — document UI flow and page routes as implemented

## User stories covered

- 3 — Storage settings page links to operator docs
- 24 — Firewall and NAT documentation
- 25 — Dedicated storage docs section separate from CI
- 26 — Full onboarding guide without support
- 27 — CI docs cross-link clarifying separate fleets

## Notes

- Follow CI docs conventions for page structure, navigation component, and i18n section titles where applicable.
- Do not duplicate entire encrypted-replica-storage PRD — link to product docs for placement tiers and RF=4 model.
