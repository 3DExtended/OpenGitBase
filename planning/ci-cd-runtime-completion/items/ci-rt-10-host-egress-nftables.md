# Host egress nftables + compose allowlist seed

## Metadata

- ID: ci-rt-10
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-runtime-completion.md

## Parent

[PRD: CI/CD Runtime Completion](../../../docs/prd/ci-cd-runtime-completion.md)

## What to build

Replace script-only URL preflight as the primary egress mechanism with host-enforced nftables on the MicroVM tap: default DROP egress, allowlisted domains resolved via host DNS into timed ipsets. Log runtime denials with guidance to submit a **Domain Allowance Request**.

Production starts with empty platform allowlist (deny-all). Add a compose-only seed script for minimal dev allowlist entries. Compose effective allowlists for `organization-self-hosted` using `CI_ORGANIZATION_ID` from ci-rt-03.

## Acceptance criteria

- [ ] Guest egress blocked by default on tap interface for Firecracker jobs
- [ ] Allowlisted domains resolve to ipset entries; matching egress succeeds in tracer test
- [ ] Denied egress produces log line citing domain and allowance workflow
- [ ] Compose seed script documents and applies dev-only allowlist entries
- [ ] Org-owned repo self-hosted jobs merge platform and org allowlists

## Blocked by

- [ci-rt-06-firecracker-launcher.md](./ci-rt-06-firecracker-launcher.md) (ci-rt-06)

## User stories covered

- 20 — Egress enforced on host at tap interface
- 21 — Default deny on `ogb-hosted`
- 22 — Clear runtime denial logs
- 23 — Production starts with empty platform allowlist
- 24 — Compose seed script for dev egress
- 26 — Org egress composed for org-owned self-hosted jobs

## Notes

- Static script URL scan may remain as supplementary signal; not primary enforcement.
- Org egress UI is ci-rt-14.
